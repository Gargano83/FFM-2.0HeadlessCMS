# DAMIHeadlessCMS

**DAMIHeadlessCMS** è un CMS headless-ish per .NET 10, distribuito come insieme
di librerie (una Razor Class Library + supporto dati/scaffolding) da montare
dentro una qualsiasi applicazione ASP.NET Core MVC host. Si occupa **solo del
backoffice** (autenticazione, CRUD, struttura, pagine, menu): il rendering
front-end dei contenuti resta sempre responsabilità del progetto host.

L'approccio è **metadata-driven**: non viene generato codice C#/Razor per le
entità gestite. Tutto il comportamento (quali tabelle sono gestibili, quali
colonne mostrare, con quale editor, se sono obbligatorie, ecc.) è pilotato da
metadati salvati nello schema `cms.*` del database, popolati da un wizard di
scaffolding che legge la struttura reale del database via `sys.*`.

## Indice

- [Cosa fa il CMS](#cosa-fa-il-cms)
- [Architettura e progetti](#architettura-e-progetti)
- [Setup e migrazioni](#setup-e-migrazioni)
- [Integrazione in un progetto host](#integrazione-in-un-progetto-host)
- [Funzionalità implementate](#funzionalità-implementate)
  - [1. Scaffolding — mappare le tabelle del database](#1-scaffolding--mappare-le-tabelle-del-database)
  - [2. CRUD generico sui dati](#2-crud-generico-sui-dati)
  - [3. Identity e ruoli](#3-identity-e-ruoli)
  - [4. Pagine custom a blocchi](#4-pagine-custom-a-blocchi)
  - [5. Menu di navigazione](#5-menu-di-navigazione)
  - [6. Editor avanzati (file, rich text, autocomplete FK)](#6-editor-avanzati-file-rich-text-autocomplete-fk)
  - [7. Localizzazione legacy "a chiave condivisa"](#7-localizzazione-legacy-a-chiave-condivisa)
  - [8. Modulo FFM — componenti Angular/Syncfusion dedicati](#8-modulo-ffm--componenti-angularsyncfusion-dedicati)
- [Sicurezza: come viene evitato SQL injection nel CRUD dinamico](#sicurezza-come-viene-evitato-sql-injection-nel-crud-dinamico)
- [Roadmap](#roadmap)

## Cosa fa il CMS

In sintesi, DAMIHeadlessCMS permette di:

1. **Puntarlo su un database SQL Server esistente** (anche legacy, con schemi
   multipli e senza vincoli di integrità fisici) e scegliere, tramite un
   wizard nel backoffice, quali tabelle rendere gestibili da interfaccia.
2. Ottenere automaticamente un **CRUD completo** (elenco, creazione,
   modifica, cancellazione) su quelle tabelle, senza scrivere una riga di
   codice per ciascuna di esse: editor, validazioni di base, lookup sulle
   foreign key e upload file vengono dedotti dal tipo di colonna e restano
   sempre personalizzabili dal backoffice stesso.
3. Gestire in modo separato i **contenuti "editoriali"** del sito (pagine a
   blocchi, menu di navigazione) che normalmente non hanno una tabella
   applicativa dedicata.
4. Fare tutto questo dietro un **sistema di login e ruoli dedicato**
   (`CmsAdmin` / `CmsOperator` / `CmsEditor`), indipendente da un eventuale
   sistema di autenticazione già presente nel progetto host per gli utenti
   finali.

Il CMS **non** si occupa di:

- Renderizzare il contenuto sul sito pubblico: espone solo dati (tabelle,
  pagine, alberatura di menu) che l'app host consuma come preferisce (Razor
  Pages, API + SPA, ecc.).
- Ospitare asset statici esterni (es. build compilate di altri strumenti):
  restano sotto la responsabilità del progetto host.

## Architettura e progetti

| Progetto | Tipo | Responsabilità |
|---|---|---|
| `DAMIHeadlessCMS.Core` | Class Library (POCO) | Entità di dominio (`EntityDefinition`, `FieldDefinition`, `CmsPage`, `CmsMenu`, `CmsMenuItem`, `LocalizationSource`) ed enum (`EditorType`, `MenuTargetType`). Nessuna dipendenza da EF Core: riutilizzabile anche da un eventuale layer di servizi/API. |
| `DAMIHeadlessCMS.Data` | Class Library | `CmsDbContext` (schema `cms`), configurazioni Fluent API, migrazioni EF Core, Identity dedicato (`CmsUser`/`CmsRole`), extension method `AddDAMIHeadlessCMSData`/`AddDAMIHeadlessCMSIdentity`. |
| `DAMIHeadlessCMS.Scaffolding` | Class Library | Lettura schema DB via T-SQL diretto su `sys.*` (`SqlServerSchemaReader`), inferenza dell'`EditorType` (`EditorTypeInferrer`), orchestrazione dello scaffold idempotente (`ScaffoldingService`). |
| `DAMIHeadlessCMS.Admin` | Razor Class Library | Tutto il backoffice: controller, view, wizard, CRUD generico, gestione utenti/menu/pagine/localizzazioni. Montata come RCL nell'app host tramite routing MVC standard. |
| `DAMIHeadlessCMS.TestHost` | App MVC | Host minimale usato per sviluppo/test end-to-end del CMS, mostra un esempio reale di integrazione. |

Il pacchetto è pensato per essere distribuito come **libreria NuGet**
(attualmente referenziato come progetti in soluzione), montabile in qualunque
applicazione ASP.NET Core MVC senza richiedere una ricompilazione ogni volta
che cambia la struttura dei dati gestiti.

### Perché "metadata-driven" e non generazione di codice

Le tabelle applicative (es. `Products`, `Orders`, tabelle legacy come
`FFM.Giocatori`) **non sono mappate come entità EF Core**: sarebbe impossibile
per una libreria riutilizzabile conoscerle a compile-time. Vengono invece
lette/scritte a runtime con **SQL dinamico parametrico**, costruito solo a
partire da identificatori (nomi tabella/colonna) già validati e persistiti in
`cms.EntityDefinition`/`cms.FieldDefinition` — mai da input utente diretto.
Questo consente di aggiungere il supporto a una nuova tabella semplicemente
rilanciando lo scaffolding, senza mai ricompilare o distribuire una nuova
versione della libreria.

## Setup e migrazioni

1. SDK .NET 10 installato:
   ```
   dotnet --version
   ```
2. Ripristina i pacchetti dalla root della solution (`DAMIHeadlessCMS.slnx`):
   ```
   dotnet restore
   ```
3. **Connection string per il design-time.** `DAMIHeadlessCMS.Data` è una
   libreria priva di `Program.cs`: per generare/applicare le migration, gli
   strumenti `dotnet ef` usano `CmsDbContextFactory`
   (`IDesignTimeDbContextFactory<CmsDbContext>`), che legge la connection
   string con lo stesso pattern a due file usato dall'host (vedi sotto):
   `src/DAMIHeadlessCMS.Data/appsettings.json` è versionato ma contiene solo un
   placeholder (`CAMBIAMI`); crea accanto
   `src/DAMIHeadlessCMS.Data/appsettings.Development.json` (file locale,
   **mai** committato — vedi `.gitignore`) con la stessa struttura e i tuoi
   dati reali:
   ```json
   {
     "ConnectionStrings": {
       "Default": "Server=localhost,1433;Database=NomeDb;User Id=sa;Password=...;TrustServerCertificate=True;"
     }
   }
   ```
   In alternativa, senza toccare file, imposta la variabile d'ambiente
   `DAMIHEADLESSCMS_CONNECTIONSTRING`.

   > Attenzione alla sintassi: `Trusted_Connection=True` (Windows
   > Authentication) e `User Id`/`Password` (SQL Authentication) sono **due
   > modalità alternative**, non vanno mai combinate. Per un'istanza con
   > utente `sa` (es. container Docker):
   > `Server=host,1433;Database=NomeDb;User Id=sa;Password=...;TrustServerCertificate=True;`

4. Dalla cartella `src/DAMIHeadlessCMS.Data`, applica le migration esistenti
   al database (creano solo lo schema `cms.*`: tabelle applicative esistenti
   non vengono toccate):
   ```
   dotnet tool install --global dotnet-ef   # se non già installato
   dotnet ef database update
   ```
5. Per generare una nuova migration dopo aver modificato le entità di
   `DAMIHeadlessCMS.Core`/configurazioni in `DAMIHeadlessCMS.Data`:
   ```
   dotnet ef migrations add NomeMigrazione
   ```
   Non serve `--startup-project`: la design-time factory rende
   `DAMIHeadlessCMS.Data` autosufficiente per gli strumenti EF.

## Integrazione in un progetto host

Esempio minimale (vedi `DAMIHeadlessCMS.TestHost/Program.cs` per un caso reale
completo):

```csharp
using DAMIHeadlessCMS.Admin.Extensions;
using DAMIHeadlessCMS.Data.Extensions;
using DAMIHeadlessCMS.Data.Identity;
using DAMIHeadlessCMS.Scaffolding.Extensions;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")!;

// Stessa connection string per CmsDbContext (metadati in schema cms.*) e per
// il repository generico (tabelle applicative in dbo/altri schemi): vivono
// nello stesso database fisico.
builder.Services.AddDAMIHeadlessCMSData(connectionString);
builder.Services.AddDAMIHeadlessCMSAdmin(connectionString);
builder.Services.AddDAMIHeadlessCMSScaffolding(connectionString);
builder.Services.AddDAMIHeadlessCMSIdentity();

builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDefaultControllerRoute();

// Crea i ruoli CmsAdmin/CmsEditor/CmsOperator e, se configurati, i primi
// utenti per ciascun ruolo.
await DAMIHeadlessCMSIdentitySeeder.SeedAsync(app.Services, app.Configuration);

app.Run();
```

Il backoffice risponde per default sotto il prefisso di route **`/dami`**
(login: `/dami/account/login`). Per il seed dei primi utenti, in `appsettings`
(ogni blocco è indipendente e facoltativo — quello di `SeedAdmin` è comunque
**fortemente consigliato** al primo avvio: senza un admin pre-esistente
nessuno potrebbe accedere al backoffice per crearne uno):

```json
{
  "DAMIHeadlessCMS": {
    "SeedAdmin":    { "Email": "admin@example.com",    "Password": "Pa$$w0rd1" },
    "SeedEditor":   { "Email": "editor@example.com",   "Password": "Pa$$w0rd1" },
    "SeedOperator": { "Email": "operator@example.com", "Password": "Pa$$w0rd1" }
  }
}
```

> **Dove metterlo**: `appsettings.json` (versionato) contiene solo placeholder
> vuoti per questi blocchi — di proposito, così il seeding resta disattivato
> finché non lo configuri esplicitamente. Le credenziali reali vanno in
> `appsettings.Development.json` (o l'equivalente per il tuo ambiente), **mai
> committato** — vedi `.gitignore` e la fase 13 di
> [`docs/ROADMAP.md`](docs/ROADMAP.md) per il perché di questo pattern a due
> file.

## Funzionalità implementate

### 1. Scaffolding — mappare le tabelle del database

Percorso backoffice: **Struttura → Scaffolding** (`/dami/scaffolding`,
riservato a `CmsAdmin`; la vista di sola lettura della struttura di
un'entità già scaffoldata, descritta più sotto, è invece accessibile anche a
`CmsOperator`).

Un wizard a due step, in linea con la preferenza per configurazioni "a
singolo passaggio" quando possibile:

1. **Selezione tabelle**: elenco di tutte le tabelle del database (raggruppate
   per schema, con indicazione di quelle già configurate), letto tramite
   query dirette su `sys.tables`/`sys.columns`/`sys.foreign_keys`
   (`SqlServerSchemaReader`) — nessuna dipendenza da API interne di EF Core,
   per stabilità tra versioni.
2. **Configurazione e salvataggio**: per ogni tabella selezionata viene
   mostrata un'anteprima dei campi con `EditorType` dedotto automaticamente
   dal tipo SQL (`EditorTypeInferrer`), etichetta, visibilità in lista/form,
   obbligatorietà ed eventuale associazione a una `LocalizationSource`. Tutto
   è modificabile prima di un unico salvataggio finale.

Lo scaffolding è **idempotente**: rilanciarlo sulle stesse tabelle (dal
wizard, o dal pulsante "Aggiorna struttura" nella vista Struttura di
un'entità) aggiorna i metadati strutturali (nuove colonne, tipi, FK) ma
**preserva sempre le personalizzazioni già fatte** (etichette, editor,
visibilità, ordinamento). La struttura fisica del database, invece, **non è
mai modificabile dal backoffice**: viene solo letta.

Ogni entità scaffoldata ha anche una vista **Struttura** di sola lettura
(`/dami/{entityId}/structure`) che mostra colonne, tipi, PK/identity/FK e
configurazione corrente, utile per verificare cosa è stato mappato.

**Raggruppamento** (`EntityDefinition.GroupName`): oltre a `DisplayName` e
`Icon`, ogni entità ha un'etichetta di gruppo usata per organizzare sia la
sezione "Dati" della sidebar sia i riquadri "Entità gestite" della dashboard
(vedi [§9](#9-dashboard-post-login-e-log-di-audit)). Al primo scaffolding di
una tabella il gruppo di default è lo **schema** del database (`FFM`, `dbo`,
...), ma è liberamente rinominabile nello step 2 del wizard — utile per
ottenere raggruppamenti più mirati di quelli forniti dallo schema fisico
(es. "Statistiche" per l'insieme delle tabelle `FFM.*Statistiche`). Come per
`DisplayName`, una rinomina manuale viene sempre preservata dai successivi
ri-scaffolding idempotenti.

#### 1.1 Routing di dettaglio per i record (opzionale)

Per qualunque entità scaffoldata, nel wizard è possibile configurare
opzionalmente due proprietà pensate per costruire un URL di dettaglio per i
singoli record (es. la scheda di una categoria, non l'intero listato):

- **Prefisso URL** (`EntityDefinition.DetailRoutePrefix`, es. `/categorie`):
  un percorso interno del sito.
- **Campo chiave** (`EntityDefinition.DetailKeyFieldId`): la colonna che
  identifica il record nell'URL (es. uno `Slug` dedicato). Se non impostato,
  convenzione: si usa la chiave primaria.

L'URL del singolo record è quindi `{DetailRoutePrefix}/{valore(campo
chiave)}`. Il **prefisso** partecipa allo stesso controllo di unicità
introdotto in fase 12 (`InternalUrlPath`): non può collidere con lo slug di
una `CmsPage`, un percorso `ExternalUrl` di menu, o il prefisso di un'altra
entità — verificato sia a livello applicativo
(`ScaffoldingWizardController`, con messaggio d'errore nel wizard) sia con un
indice univoco filtrato a livello database (difesa in profondità).

**Cosa NON fa questa funzionalità** — per scelta, non per limite tecnico:
- **Non genera pagine né rotte**: il CMS valida e conserva solo i metadati
  (prefisso + campo chiave). Far corrispondere un URL in ingresso al
  prefisso giusto ed estrarne il record resta responsabilità del progetto
  host — coerente con l'architettura "il CMS genera/valida i metadati,
  l'host renderizza", la stessa già adottata per pagine e menu.
- **Non garantisce l'unicità dei valori del campo chiave riga per riga**: è
  una responsabilità dei dati stessi (la tabella applicativa sottostante,
  spesso legacy), non qualcosa che il CMS impone senza alterare uno schema
  fisico che non gli appartiene — stesso principio di "integrità solo
  applicativa" già seguito per le tabelle del modulo FFM.

#### 1.2 Riferimento manuale a un'altra tabella (FK senza vincolo fisico)

Quando esiste un vincolo FK fisico nel database, il wizard rileva
automaticamente destinazione e colonna etichetta. Molte tabelle legacy, però,
esprimono relazioni solo a livello applicativo, senza alcun vincolo (tipico
delle tabelle di lookup condivise, es. una `WN_LOOKUP` usata da più colonne
per liste diverse). Per questi casi, ogni campo può essere configurato
**manualmente** nel wizard con:

- **Tabella di destinazione**: scelta tra qualunque tabella del database, non
  solo quelle già scaffoldate — se non lo è ancora, viene scaffoldata
  automaticamente (con impostazioni di default) nello stesso salvataggio,
  cascata inclusa nella stessa chiamata a `ScaffoldTablesAsync`.
- **Colonna etichetta**: quale colonna mostrare nell'autocomplete.
- **Filtri** (`FieldDefinition.ForeignKeyFiltersJson`, opzionali, in AND): una
  o più condizioni `colonna / operatore / valore` (stessi 6 operatori di
  `QueryFilterOperator`) applicate alle opzioni dell'autocomplete — es. due
  campi diversi che puntano entrambi a `WN_LOOKUP` ma devono offrire
  sottoinsiemi diversi (`LK_LG_ID = 1` per le stagioni, `LK_LG_ID = 32` per le
  competizioni). Non applicati alla risoluzione dell'etichetta di un valore
  già scelto (la chiave primaria è già univoca di per sé).

La configurazione manuale non viene mai applicata automaticamente sopra una
FK fisica già rilevata: si attiva solo se esplicitamente impostata per quel
campo nel wizard.

### 2. CRUD generico sui dati

Percorso backoffice: **Dati** (`/dami`, sidebar dinamica in base alle entità
scaffoldate).

Per ogni tabella scaffoldata, `GenericEntityController` espone Elenco
(paginato), Creazione, Modifica, Cancellazione — instradati per
`EntityDefinition.Id` (Guid, non per nome tabella: nomi possono ripetersi tra
schemi diversi, es. una tabella `Lega` sia in `FFM` che in `TEST`).

L'accesso ai dati (`GenericEntityRepository`) è **SQL dinamico parametrico**:
i **nomi** di tabella/colonna sono sempre presi da `EntityDefinition`/
`FieldDefinition` (mai da input utente) e quotati come identificatori SQL; i
**valori** sono sempre passati come parametri SQL veri (mai concatenati come
stringa). Vedi la sezione [Sicurezza](#sicurezza-come-viene-evitato-sql-injection-nel-crud-dinamico).

Il form di editing (`_EntityForm.cshtml`) sceglie automaticamente l'editor
Razor giusto in base all'`EditorType` del campo: testo, area di testo, rich
text (Quill), numero, checkbox, data/data-ora, select con autocomplete su FK,
upload file, campo nascosto.

**Lettura filtrata/ordinata per consumatori esterni** — oltre a
`GetListAsync` (paginazione per PK, uso CRUD di backoffice) e `GetByIdAsync`
(singola riga), `IGenericEntityRepository.QueryAsync(entity, filters, sort,
top)` permette a un'applicazione host di leggere un sottoinsieme di righe
secondo criteri noti a compile-time (es. "ultimi N record attivi di una
categoria, più recenti prima") senza dover scrivere SQL a mano: stesso
whitelisting di identificatori e stessa parametrizzazione dei valori delle
altre operazioni. Filtro e ordinamento operano solo su colonne **non
localizzate** (il valore fisico di una colonna localizzata è una chiave
intera verso la `LocalizationSource`, non il testo tradotto — filtrarci/
ordinarci sopra richiederebbe una semantica diversa, non supportata: viene
sollevata `InvalidOperationException` se referenziata).

Per liste pubbliche con **paginazione reale** (non solo un `TOP N`),
`QueryPageAsync(entity, filters, sort, page, pageSize)` applica lo stesso
filtro/ordinamento di `QueryAsync` ma con `OFFSET`/`FETCH` e conteggio totale
delle righe (via `COUNT(*) OVER()`, un'unica query) — restituisce lo stesso
`GenericEntityPage` di `GetListAsync`.

Per risolvere uno slug/url leggibile (es. `"regole"`) nella chiave primaria
della riga corrispondente, quando quella colonna è essa stessa localizzata
(quindi non filtrabile con `QueryAsync`/`QueryPageAsync`, che sulle colonne
localizzate sollevano eccezione di proposito),
`FindIdByLocalizedValueAsync(entity, columnName, value)` fa il percorso
inverso di `GetLookupOptionsAsync`/`GetLookupLabelAsync`: cerca per **testo
tradotto** (join sulla `LocalizationSource`) e ritorna l'id, non il
contrario.

### 3. Identity e ruoli

ASP.NET Core Identity **dedicato al backoffice**, separato da un eventuale
Identity dell'app host per gli utenti finali: vive nello schema `cms.*` (le
tabelle standard `AspNetUsers`/ecc. sono rinominate senza prefisso, es.
`cms.User`, `cms.Role`) e serve **esclusivamente** ad autenticare l'accesso al
backoffice.

Tre ruoli:

- **`CmsAdmin`**: accesso completo — dati, struttura/scaffolding,
  localizzazioni, gestione utenti, modulo FFM.
- **`CmsOperator`**: ruolo intermedio. Lettura/scrittura piena su **Dati**,
  **Pagine** e **Menu** (le stesse sezioni di `CmsEditor`); in più, accesso in
  **sola lettura** a Struttura/Scaffolding (solo visualizzazione, non può
  rieseguire lo scaffolding), Utenti, Localizzazioni e alle pagine dedicate
  del modulo FFM (Database Giocatori, Squadre/Rosa). Nelle relative view i
  controlli di scrittura (pulsanti "Nuovo"/"Modifica"/"Elimina", campi form,
  toolbar della Grid Angular) vengono nascosti o disabilitati per questo
  ruolo; l'enforcement reale resta comunque lato server, con policy di
  autorizzazione dedicate per ciascuna di queste sezioni
  (`StructureViewPolicy`, `UsersViewPolicy`, `LocalizationViewPolicy`,
  `FfmViewPolicy`) distinte dalla policy di scrittura (`AdminPolicy`).
- **`CmsEditor`**: solo CRUD sui dati delle entità già scaffoldate, pagine e
  menu (niente struttura, niente gestione utenti, niente localizzazioni,
  niente modulo FFM).

Login/logout dedicati (`/dami/account/login`), cookie di autenticazione
proprio (`DAMIHeadlessCMS.Auth`), seeding dei primi utenti da configurazione al
primo avvio (indispensabile almeno per l'admin: senza un admin pre-esistente
nessuno potrebbe accedere al backoffice per crearne uno). Gestione utenti
(`/dami/users`) in sola lettura per `CmsOperator`, in lettura/scrittura per
`CmsAdmin`.

### 4. Pagine custom a blocchi

Percorso backoffice: **Contenuti → Pagine** (`/dami/pages`).

`CmsPage` rappresenta contenuti editoriali non legati a una tabella
applicativa (es. "Chi siamo"), con:

- Slug univoco **globalmente** (non solo tra fratelli: due pagine non possono
  mai avere lo stesso slug, indipendentemente da dove si trovano nella
  gerarchia — così un URL costruito annidando gli slug della gerarchia
  `ParentId`, es. `/livello1/livello2/pagina`, non può mai collidere con un
  altro), titolo, pagina genitore (struttura ad albero, con controllo
  anti-ciclo: una pagina non può diventare discendente di una propria
  sotto-pagina), stato pubblicato/bozza, ordinamento.
- Contenuto strutturato come **JSON a blocchi** (`ContentJson`), editabile con
  drag&drop (SortableJS) tra tre tipi di blocco:
  - `html`: testo/HTML libero, con editor rich text Quill — **incluse
    immagini**: il pulsante immagine carica il file tramite lo stesso
    `IFileStorageProvider` dei campi `EditorType.File` (endpoint dedicato
    `POST /dami/pages/upload-image`, sottocartella `wwwroot/uploads/pages`
    dell'host), inserendo nell'HTML solo l'URL — mai un'immagine incorporata
    come base64.
  - `entityList`: riferimento a un'entità già scaffoldata (più titolo
    opzionale sopra l'elenco e un numero massimo di righe, di default 50,
    fino a un tetto di 200 — stesso limite applicato da
    `IGenericEntityRepository.GetListAsync`). Le colonne mostrate sono quelle
    marcate "in elenco" nello scaffolding (stesso criterio già usato dalla
    griglia dati del backoffice), con le eventuali FK già risolte in
    etichetta leggibile.
  - `component`: riferimento generico a un componente esterno (tag + config
    JSON libera), pensato per essere risolto dall'app host (es. un componente
    Angular montato dinamicamente — vedi [§8](#8-modulo-ffm--componenti-angularsyncfusion-dedicati)).

Il rendering front-end dei blocchi è a carico dell'app host: il TestHost
incluso in questo repository lo implementa già per `html` ed `entityList`
(`PagesController.Show` risolve i blocchi lato server — per `entityList`
significa una query al database tramite `IGenericEntityRepository` — prima
di passarli, già pronti, alla view `Show.cshtml`; vedi anche
`LegacyContentReader.GetRowsForDisplayAsync`). Il tipo `component` resta
intenzionalmente non gestito lì: non è mai servito un caso d'uso concreto su
una `CmsPage` nativa (il modulo FFM monta i suoi componenti Angular tramite
le proprie view dedicate, non attraverso pagine CMS).

Una `CmsPage` è quindi il modo più rapido per comporre, **senza scrivere
codice**, una pagina che mescola testo/immagini libere e contenuto già
scaffoldato — utile per arricchire pagine che altrimenti richiederebbero un
template ad-hoc solo per una sezione editoriale accessoria. Per pagine con
logica di presentazione molto specifica (es. la pagina Statistiche, con le
sue sezioni per competizione — vedi
[§8](#8-modulo-ffm--componenti-angularsyncfusion-dedicati)) resta preferibile
un template ad-hoc lato TestHost, come già fatto: `CmsPage`/`entityList` non
sostituisce quel pattern, lo completa per i casi più semplici.

### 5. Menu di navigazione

Percorso backoffice: **Contenuti → Menu** (`/dami/menus`).

Un'app host può avere più menu distinti, identificati per nome (es.
`main-nav`, `footer`). Ogni menu è un albero di `CmsMenuItem`, editabile con
un editor drag&drop (SortableJS) che supporta riordino e annidamento in
un'unica sessione, salvata poi in un solo passaggio "full replace": il client
invia l'intero albero corrente e il server ricostruisce da zero le righe di
`cms.MenuItem` per quel menu.

Ogni voce di menu ha:

- **Etichetta** (`Label`).
- **Tipo di destinazione** (`TargetType`):
  - `Page`: punta a una `CmsPage` tramite il suo `Slug`.
  - `Entity`: punta all'elenco di un'entità scaffoldata (`schema.tabella`).
  - `ExternalUrl`: URL libero, gestito interamente dall'app host.
- **`OpenInNewTab`** (bool): flag esplicito, configurabile per singola voce
  **indipendentemente dal tipo di destinazione**, che segnala all'app host
  l'intenzione di aprire il link in una nuova scheda (`target="_blank"`).
  Utile ad esempio per collegamenti a contenuti esterni alla navigazione
  principale (documentazione, regolamenti, ecc.).

> **Importante**: il CMS genera e persiste **solo l'alberatura** del menu
> (incluso il flag `OpenInNewTab`). Il rendering HTML effettivo della
> navigazione — e quindi l'applicazione pratica di `target="_blank"` — è
> sempre responsabilità del progetto host, che consuma `CmsMenu`/
> `CmsMenuItem` per costruire il proprio markup di navigazione front-end.

Un caso d'uso concreto già previsto: pubblicare un contenuto statico esterno
(es. un sito Docusaurus già compilato, servito dall'host sotto una cartella
tipo `wwwroot/regolamento`) semplicemente creando una voce di menu con
`TargetType = ExternalUrl`, `TargetValue = "/regolamento"` e
`OpenInNewTab = true` — senza alcuna integrazione aggiuntiva lato CMS.

#### Unicità degli URL

Le voci `TargetType = Page` sono al sicuro "per costruzione": lo slug si
sceglie da una dropdown alimentata dalle `CmsPage` esistenti, il cui slug è
già univoco globalmente (vedi sopra). Il caso da validare esplicitamente è
`TargetType = ExternalUrl` quando il valore è un **percorso interno** del sito
(inizia con `/`, ma non è protocol-relative `//host/...` — i link davvero
esterni come `https://...` non competono per lo spazio di URL del CMS e non
vengono controllati). Il salvataggio del menu (`MenusController.Save`) rifiuta
(HTTP 400, messaggio mostrato nell'editor ad albero) un percorso interno
`ExternalUrl` duplicato: tra le voci dello stesso salvataggio, rispetto a un
altro menu, o rispetto allo slug di una `CmsPage` esistente (in quest'ultimo
caso la voce corretta è di tipo "Pagina"). Speculare, la creazione/modifica di
una `CmsPage` (`PagesController`) rifiuta uno slug che collide con un percorso
interno già usato da una voce `ExternalUrl`. La logica di normalizzazione e
confronto è centralizzata in `InternalUrlPath`
(`DAMIHeadlessCMS.Admin.Utilities`), condivisa dai due controller.

> Questo controllo copre lo spazio di URL "curato a mano" da Pagine e Menu.
> Per il routing di dettaglio sui singoli record di una tabella scaffoldata
> (es. `/categorie/{slug}`), vedi la sezione 1.1 più sotto: quello spazio di
> URL partecipa allo stesso controllo di unicità.

### 6. Editor avanzati (file, rich text, autocomplete FK)

- **Upload file** (`EditorType.File`): astratti tramite `IFileStorageProvider`
  (default: `LocalFileStorageProvider`, salva in `wwwroot/uploads` **dell'app
  host**, così i file sono serviti direttamente da `UseStaticFiles()` senza
  configurazione aggiuntiva). Per colonne `varbinary`/`binary`/`image` i byte
  vengono invece salvati direttamente nella colonna del database. Per
  cambiare storage (es. Azure Blob) basta registrare una propria
  implementazione di `IFileStorageProvider` **dopo**
  `AddDAMIHeadlessCMSAdmin(...)`: l'ultima registrazione vince nel container DI.
- **Rich text** (`EditorType.RichText`): editor Quill integrato nel form del
  CRUD generico e nei blocchi HTML delle pagine.
- **Autocomplete su foreign key** (`EditorType.Select`): ricerca live con
  debouncing lato client, risolta lato server tramite
  `/dami/lookup/{fieldId}` (suggerimenti filtrati) e
  `/dami/lookup/{fieldId}/label` (etichetta del valore già selezionato, per
  pre-popolare il campo in modifica). La colonna usata come etichetta è letta
  da `FieldDefinition.ForeignKeyDisplayColumn`, già risolta in fase di
  scaffolding — non ricalcolata a runtime.
- **Password** (`EditorType.Password`): pensato per colonne come
  `WN_UTENTI.UT_Password` (in chiaro sarebbero altrimenti mostrate/modificabili
  come testo semplice nel CRUD generico). Da assegnare manualmente a un campo
  nel wizard (nessuna inferenza automatica: non è deducibile dal solo tipo SQL).
  Comportamento:
  - Nel form si presenta come `<input type="password">` **mai precompilato**:
    l'hash esistente non viene mai rimandato al browser, nemmeno nell'HTML
    sorgente della pagina di modifica.
  - In **modifica**, campo lasciato vuoto = "non cambiare la password" (la
    colonna viene esclusa dallo `UPDATE`, non svuotata). Non è quindi mai
    obbligatorio in modifica, anche se `IsRequired` è true (lo è solo in
    creazione).
  - Il testo in chiaro inserito non viene **mai** scritto così com'è: prima
    della `INSERT`/`UPDATE` viene trasformato tramite
    `FieldDefinition.PasswordHashFunction`, anch'esso configurabile per campo
    nel wizard (voce di testo libero accanto al selettore dell'editor,
    visibile solo quando l'editor è "Password"):
    - **Valorizzato** (es. `dbo.udf_Encrypt`): la trasformazione avviene
      **lato database**, chiamando quella funzione scalare SQL esistente
      (`SELECT {funzione}(@PlainValue)`) — utile per riprodurre esattamente
      l'hashing di una funzione legacy già usata altrove (tipicamente in fase
      di login) e garantire che i valori combacino bit per bit. È un
      metadato impostabile solo da chi ha accesso al wizard (`CmsAdmin`),
      mai da input utente a runtime.
    - **Vuoto** (default): hashing SHA-512 calcolato in .NET, stesso formato
      testuale `"0x" + esadecimale maiuscolo` prodotto da
      `CONVERT(varchar, HASHBYTES('SHA2_512', ...), 1)`, ma senza dipendere
      da nessuna funzione SQL — comodo per colonne password create ex novo
      dal CMS su un progetto che non ha (ancora) una funzione di hashing
      legacy da riusare.
  - Nella vista elenco e ovunque il valore compaia fuori dal proprio form, è
    sempre mascherato (`••••••••`), anche se la colonna è marcata
    `ShowInList` (comunque sconsigliato per un campo password: meglio
    lasciarla visibile solo nel form).

  > Esempio concreto — `WN_UTENTI.UT_Password`, che il login del TestHost
  > verifica con una chiamata SQL diretta a `dbo.udf_Encrypt(@Password)` (vedi
  > [§9](#9-dashboard-post-login-e-log-di-audit) → Area Riservata): impostando
  > `EditorType = Password` e `PasswordHashFunction = dbo.udf_Encrypt` su
  > quel campo, una modifica della password dal backoffice produce lo stesso
  > hash che il login si aspetta di trovare, senza reimplementare
  > l'algoritmo in .NET.

#### 6.1 Arricchire un template ad-hoc con un blocco curato da backoffice

Alcune pagine pubbliche (Statistiche, Squadre — vedi
[§8](#8-modulo-ffm--componenti-angularsyncfusion-dedicati)) hanno logica di
presentazione troppo specifica per il blocco `entityList` generico (sezioni
multiple, pivot, formattazioni ad hoc su più tabelle): restano — a ragione —
controller e view dedicati lato host, non `CmsPage`. Ma capita comunque che
serva un'introduzione testuale/immagini sopra quel contenuto codificato,
editabile senza toccare il codice: il pattern usato per la pagina Statistiche
(che replica l'"Albi d'oro" del sito legacy) è riusabile per qualunque altro
template ad-hoc — applicato anche a Comunicazioni (slug di supporto
`comunicazioni-intro`, mostrato solo nell'elenco non filtrato: filtrando per
categoria non avrebbe senso un'introduzione pensata per l'intera sezione).

**Idea**: il controller ad-hoc non prova a "diventare" una CmsPage — resta
esattamente com'era — ma legge il blocco `html` di una `CmsPage` di supporto
(creata da backoffice, slug dedicato, **mai linkata nel menu**: è un
contenitore di contenuto, non una pagina da visitare) e lo inserisce sopra il
proprio contenuto codificato.

1. **Crea la CmsPage di supporto** da `/dami/pages/create`: solo un blocco
   `html` con testo/immagini (nessun blocco `entityList`/`component`, qui non
   servono — quel contenuto lo fornisce già il template ad-hoc). Slug
   dedicato e riconoscibile, es. `statistiche-intro`/`comunicazioni-intro` —
   **non** lo slug della pagina stessa (`statistiche`, `comunicazioni`):
   collisione impossibile comunque, dato che quelle rotte sono già
   intercettate dalla rotta convenzionale del controller dedicato,
   registrata prima della rotta generica delle CmsPage — vedi `Program.cs` —
   quindi una CmsPage con quello slug non sarebbe mai raggiungibile, ma è
   comunque buona norma tenerli distinguibili a colpo d'occhio in
   `/dami/pages`.
2. **Nel controller ad-hoc**, inietta `LegacyContentReader` e leggi il
   ContentJson per slug:
   ```csharp
   var introContentJson = await _content.GetPageContentJsonAsync("statistiche-intro", ct);
   model.IntroHtml = CmsPageContentParser.GetHtmlBlocksConcatenated(introContentJson);
   ```
   `CmsPageContentParser` (usato anche da `PagesController.Show` per le
   CmsPage native) fa il parsing di basso livello del ContentJson — qui si
   usa solo `GetHtmlBlocksConcatenated`, che concatena i blocchi `html`
   ignorando gli altri tipi. Se la pagina di supporto non esiste o non è
   pubblicata, restituisce `null`: il template ad-hoc resta perfettamente
   funzionante anche senza introduzione.
3. **Nella view**, stesso markup del blocco `html` di una CmsPage nativa:
   ```cshtml
   @if (!string.IsNullOrWhiteSpace(Model.IntroHtml))
   {
       <div class="cms-block cms-block-html mb-4">
           @Html.Raw(Model.IntroHtml)
       </div>
   }
   ```

Zero rischio per il template esistente (nessuna riga della logica dati
originale viene toccata), editing dell'introduzione interamente da
backoffice, stesso identico editor rich text (con immagini) delle CmsPage
native.

### 7. Localizzazione legacy "a chiave condivisa"

Percorso backoffice: **Struttura → Localizzazioni** (`/dami/localization-sources`,
lettura/scrittura per `CmsAdmin`, sola lettura per `CmsOperator`).

Pensata per database legacy dove un campo intero in una tabella applicativa
**non è il valore reale**, ma un id di contenuto da risolvere in un'altra
tabella di traduzioni, filtrando per lingua (pattern tipico: colonne come
`LC_CONT_ID`/`LC_LNG_ID`/`LC_TESTO` in una tabella tipo
`WN_LOCALIZZAZIONE`, con integrità solo applicativa, **nessuna FK fisica**).

Una `LocalizationSource` descrive la forma di questa tabella (nomi di
colonna, tabella lingue, lingua di default — nessun selettore multi-lingua
nel backoffice per ora, solo lettura/scrittura nella lingua di default) e può
essere associata a uno o più `FieldDefinition` dal wizard di scaffolding.

- **Lettura**: subquery SQL correlata che risolve il testo tradotto al posto
  del valore grezzo, per la lingua di default.
- **Scrittura**: transazione ADO.NET esplicita che gestisce sia la riga di
  traduzione (inserimento per contenuto nuovo, con convenzione
  `LC_CONT_ID = LC_ID`; upsert per contenuto esistente) sia la colonna
  "contenitore" nella tabella applicativa. Il campo `RowIdColumn` della
  sorgente è obbligatorio per poter creare nuovo contenuto (altrimenti viene
  sollevata un'eccezione esplicita in fase di configurazione).

## 8. Modulo FFM — componenti Angular/Syncfusion dedicati

Percorso backoffice: sidebar **FFM** (lettura/scrittura per `CmsAdmin`, sola
lettura per `CmsOperator` — la Grid/riga di dettaglio Angular disabilita
editing, toolbar e comandi di scrittura in base a un attributo `read-only`
passato dalla view; le API REST sottostanti applicano comunque la stessa
restrizione lato server, indipendentemente dalla UI).

Alcune tabelle applicative legacy hanno esigenze di UI troppo specifiche per
il CRUD generico metadata-driven (griglie con editing inline avanzato, import
massivo da Excel, logiche di dominio non riconducibili a un semplice
`EditorType`). Per questi casi il CMS prevede un **modulo opzionale**,
`DAMIHeadlessCMS.Admin.Ffm`, che ospita pagine dedicate basate su componenti
Angular + Syncfusion (licenza community), sostituendo integrazioni legacy
"arcaiche" (iniezione di un `index.html` compilato intero dentro un'altra
pagina, configurazione via variabili JS globali).

Il modulo è **opt-in**: va abilitato esplicitamente dall'host, solo se
effettivamente ospita lo schema `FFM.*`:

```csharp
builder.Services.AddDAMIHeadlessCMSData(connectionString);
builder.Services.AddDAMIHeadlessCMSAdmin(connectionString);
builder.Services.AddDAMIHeadlessCMSFfm(connectionString);
```

E in configurazione (per la licenza community Syncfusion, mai hardcoded):

```json
{
  "DAMIHeadlessCMS": {
    "Ffm": { "SyncfusionLicenseKey": "LA-TUA-CHIAVE-COMMUNITY" }
  }
}
```

### Come funziona l'integrazione Angular

Ogni componente è compilato come **Custom Element** (Web Component nativo,
via `@angular/elements`) — non più come un'app Angular "intera" bootstrappata
sull'intera pagina. Questo significa:

- Si monta con un semplice tag HTML nella Razor view, es.
  `<dami-ffm-giocatori-grid api-base-url="/dami/ffm/api/giocatori"></dami-ffm-giocatori-grid>`.
- I parametri sono attributi HTML, non variabili globali (`window.appSettings`
  come nella vecchia integrazione).
- Il bundle è compilato come **file singolo** (`ngx-build-plus`, opzioni
  `singleBundle`+`bundleStyles`), copiato in
  `DAMIHeadlessCMS.Admin/wwwroot/ffm-widgets/{nome}/` e servito automaticamente
  dalla Razor Class Library all'indirizzo
  `_content/DAMIHeadlessCMS.Admin/ffm-widgets/{nome}/main.js`.
- Il progetto Angular sorgente vive **dentro lo stesso progetto .NET**, in
  `src/DAMIHeadlessCMS.Admin/ClientApp/syncfusion-tfl-app/` — versionato in
  git ma **esplicitamente escluso** dal `.csproj` (`Compile Remove`/
  `Content Remove`/`None Remove` su `ClientApp/**`), così MSBuild non lo
  compila, non lo pubblica e non lo include mai in un `dotnet pack`: resta
  puro codice sorgente Node/Angular, con un proprio ciclo di build separato
  (`npm install && npm run build:...`) e un proprio README con le istruzioni.
  `node_modules/`, `dist/` e `.angular/` di ogni app Angular sotto `ClientApp/`
  sono in `.gitignore`.

### 8.1 Database Giocatori (`FFM.Giocatori`)

Percorso: `/dami/ffm/giocatori`.

Griglia CRUD completa (creazione, modifica, cancellazione, ricerca, export ed
**import da Excel**) su `FFM.Giocatori`. Il backend è un modulo dedicato
(non generico) perché la logica non è riconducibile al CRUD metadata-driven:

- `FfmGiocatoriRepository` (ADO.NET parametrico) espone le operazioni CRUD e
  l'import massivo.
- `FfmGiocatoriApiController` (`/dami/ffm/api/giocatori`) espone l'API REST
  consumata dal componente Angular.
- `FfmController` (`/dami/ffm/giocatori`) serve la pagina che monta il Custom
  Element.

> ⚠️ **Comportamento ereditato dal sistema legacy**: l'import da Excel
> **sincronizza integralmente** `FFM.Giocatori` con il contenuto del file
> caricato — i giocatori presenti a database ma assenti dal file vengono
> **eliminati**. Il comportamento è stato preservato identico per continuità
> operativa; la UI richiede conferma esplicita prima di procedere.

### 8.2 Rosa Squadra (`FFM.SquadreRelGiocatori`)

Percorso: `/dami/ffm/squadre` (elenco) → `/dami/ffm/squadre/{id}/rosa` (rosa).

Il dato anagrafico di `FFM.Squadre` (presidente, allenatore, logo, nome
localizzato, ecc.) è gestito dal **CRUD generico** una volta scaffoldata la
tabella — nessun codice ad hoc duplicato per quella parte. La pagina indice
`/dami/ffm/squadre` (`FfmController.Squadre`) elenca le squadre e linka sia
la vista Edit generica (se scaffoldata) sia la pagina "Rosa". Solo la
gestione della rosa resta un modulo Angular/Syncfusion dedicato, perché la
UI (grid con riga di dettaglio espandibile, autocomplete giocatori
svincolati, pannello finanziario aggregato) non è riconducibile agli
`EditorType` standard:

- `FfmSquadraRepository` (ADO.NET parametrico) espone InfoSquadra aggregata
  (conteggi Tesserati/InRosa/ListaA/Under22 per la stagione attiva), la rosa,
  il dettaglio di un giocatore in rosa, i giocatori svincolati, e le
  operazioni di aggiunta/rimozione/aggiornamento.
- `FfmSquadreApiController` (`/dami/ffm/api/squadre/*`) espone l'API REST
  consumata dal componente Angular.
- `FfmController.Squadre`/`Rosa` servono le due pagine di backoffice.

**Localizzazione del nome squadra**: riusa **as-is** la funzione SQL legacy
`dbo.udf_Localize` (nessuna reimplementazione della logica di
localizzazione), con l'id lingua di default passato come parametro a
`AddDAMIHeadlessCMSFfm(connectionString, defaultLanguageId)`.

**Tracciamento utente legacy**: `FFM.SquadreRelGiocatori.IdUtente` viene
popolato risolvendo l'email dell'utente CMS loggato (`CmsUser.Email`)
all'Id utente corrispondente in `dbo.WN_UTENTI` (`IFfmUserResolver`,
corrispondenza 1:1 via `UT_Email`/`UT_ID`). Se l'utente CMS non ha una
corrispondenza in `WN_UTENTI`, `IdUtente` viene scritto `NULL` — l'assenza
di mapping non blocca l'operazione.

Il vecchio popup Angular Material per il dettaglio giocatore-squadra è stato
**sostituito** da una riga di dettaglio espandibile della Grid stessa
(funzionalità nativa di Syncfusion Grid): stesso risultato funzionale
(visualizzare/modificare Stato e Mesi di un giocatore in rosa), una
dipendenza in meno da mantenere nel bundle Angular.

> Il flag `AbilitaModifica` (letto da `FFM.Squadre`) è incluso nel DTO
> `InfoSquadra` così com'è, ma **non limita alcuna azione nel backoffice**:
> governa l'area riservata front-end (fuori dal perimetro del CMS), dove
> l'accesso al backoffice è già filtrato dal ruolo (`CmsAdmin` in
> lettura/scrittura, `CmsOperator` in sola lettura).

### 9. Dashboard post-login e log di audit

Percorso backoffice: **`/dami`**, la pagina che segue automaticamente il
login. Oltre all'elenco "Entità gestite" (già presente dalla fase 3), mostra:

- **Contatori riepilogativi**: entità scaffoldate, pagine (totali e
  pubblicate), voci di menu, utenti per ruolo.
- **Attività recente**: le ultime righe del log di audit (vedi sotto), in un
  pannello con altezza massima fissa e scroll interno (mini-timeline
  compatta): la card non si allunga più all'infinito al crescere del numero
  di voci.
- **Pagine recenti**: le ultime `CmsPage` create o modificate, con link
  diretto alla modifica.

I contatori sugli utenti per ruolo e le voci relative a `CmsUser` nel log di
audit sono visibili solo a `CmsAdmin`/`CmsOperator`, coerentemente con
`UsersViewPolicy` (fase 9): un `CmsEditor`, che non ha accesso alla pagina
Utenti, non vede di riflesso quelle informazioni nemmeno qui.

#### Raggruppamento delle entità (sidebar e dashboard)

Con molte tabelle scaffoldate, sia la sezione "Dati" della sidebar sia i
riquadri "Entità gestite" della dashboard raggrupperebbero rapidamente troppe
voci in un'unica lista verticale. Entrambe le viste usano quindi lo stesso
`EntityDefinition.GroupName` (vedi [§1](#1-scaffolding--mappare-le-tabelle-del-database))
per organizzare le entità in **sezioni collassabili** (accordion Bootstrap,
puramente client-side, nessuna preferenza persistita):

- **Sidebar**: un gruppo per ogni `GroupName`, con contatore. Resta aperto di
  default solo il gruppo che contiene la voce attualmente attiva (o il primo,
  se nessuna lo è); gli altri sono chiusi.
- **Dashboard**: stessa logica, cards leggermente più compatte, con un
  pulsante "Espandi tutti/Comprimi tutti" per chi preferisce vedere tutto in
  una volta.
- **Ricerca rapida**: entrambe le viste hanno una casella di filtro per nome
  entità, che nasconde le voci non corrispondenti ed espande automaticamente
  i soli gruppi con risultati.

Nessuna di queste modifiche altera i permessi di accesso alle singole
entità (`IsEnabled`, ordinamento `SortOrder`): il raggruppamento è solo
presentazionale.

#### Log di audit

`AuditLogEntry` (nuova tabella `cms.AuditLogEntry`) registra automaticamente
le operazioni di creazione/modifica/eliminazione sulle entità **CMS-native**:
`CmsPage`, `CmsMenu`, `CmsMenuItem`, `CmsUser`. La generazione avviene
interamente dentro `CmsDbContext.SaveChangesAsync` (override che legge il
`ChangeTracker` di EF Core prima di salvare): **nessun controller scrive
esplicitamente una riga di audit**, quindi non c'è rischio di dimenticarsene
in un punto nuovo — funziona automaticamente anche per le scritture fatte
tramite `UserManager`/`RoleManager` (Identity), che usano lo stesso
`CmsDbContext` internamente. L'utente che ha eseguito l'operazione si ottiene
da `IHttpContextAccessor` (registrato con `AddHttpContextAccessor()`), quindi
resta `null` fuori da una richiesta HTTP (es. seeding all'avvio, strumenti
`dotnet ef`) — previsto e non un errore.

**Scope deliberatamente limitato**:
- Copre solo le entità EF-native sopra elencate. **Non copre** le tabelle
  applicative scaffoldate (sezione "Dati"): quelle sono lette/scritte con SQL
  dinamico via ADO.NET (`IGenericEntityRepository`), fuori dal ChangeTracker
  di EF Core. Un audit su quelle richiederebbe un meccanismo separato,
  esplicitamente fuori scope in questa fase (vedi fase 14 di
  [`docs/ROADMAP.md`](docs/ROADMAP.md)).
- Non copre le tabelle di supporto di Identity (ruoli assegnati, claim,
  token): fuori scope per non generare rumore e non loggare dati sensibili
  come i token.
- `MenusController.Save` usa una strategia "full-replace" (elimina tutte le
  voci e reinserisce): un singolo salvataggio dell'albero del menu genera
  quindi più righe di audit (una per voce eliminata/creata), non un singolo
  "Update" — riflette accuratamente come funziona il salvataggio, non è un
  difetto del log.

## Sicurezza: come viene evitato SQL injection nel CRUD dinamico

Poiché le tabelle applicative non sono mappate da EF Core, `GenericEntityRepository`
costruisce SQL dinamico. Le regole seguite ovunque nel codice sono:

- I **nomi** di tabella/colonna usati in una query **provengono sempre e solo**
  da `EntityDefinition`/`FieldDefinition` (mai da input utente diretto:
  neanche i nomi di colonna del form vengono usati "a caso", sono sempre
  filtrati contro l'elenco `ShowInForm`/`ShowInList` già persistito) e vengono
  sempre quotati come identificatori SQL (`[Nome]`, con escaping delle `]`).
- I **valori** (compreso l'id nella clausola `WHERE`) sono **sempre** passati
  come `SqlParameter` tipizzati in base al tipo SQL reale della colonna, mai
  concatenati come stringa nel testo della query.
- Il `DefaultLanguageId` di una `LocalizationSource` (o l'analogo id lingua
  del modulo FFM) — gli unici "numeri" mai incorporati direttamente nel testo
  SQL anziché come parametro — sono metadati/configurazione impostati solo da
  `CmsAdmin`/in fase di avvio, non input utente a runtime.
- `FieldDefinition.PasswordHashFunction` (vedi [§6](#6-editor-avanzati-file-rich-text-autocomplete-fk))
  è l'unico punto del CRUD generico dove un **nome di funzione SQL** (non un
  identificatore di tabella/colonna quotabile) viene incorporato direttamente
  nel testo della query (`SELECT {funzione}(@PlainValue)`), sempre come testo
  libero configurato da `CmsAdmin` nel wizard — mai da input utente a runtime.
  Il valore effettivo digitato dall'utente finale (la password in chiaro)
  resta comunque sempre un `SqlParameter`, non viene mai concatenato: solo il
  *nome della funzione* è testo di configurazione fidato, esattamente come già
  avviene per nomi di tabella/colonna letti dallo schema del database.

## Roadmap

Lo stato dettagliato di ogni fase (comprese le prossime: localizzazione
multi-lingua nel backoffice e ulteriori espansioni del modulo FFM) è
tracciato in [`docs/ROADMAP.md`](docs/ROADMAP.md).

