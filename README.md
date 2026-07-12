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
   string da `src/DAMIHeadlessCMS.Data/appsettings.json` (file locale, **mai**
   committato — vedi `.gitignore`):
   ```
   cp src/DAMIHeadlessCMS.Data/appsettings.example.json src/DAMIHeadlessCMS.Data/appsettings.json
   ```
   e valorizza `ConnectionStrings:Default` con i tuoi dati reali. In
   alternativa, senza toccare file, imposta la variabile d'ambiente
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
  - `html`: testo/HTML libero, con editor rich text Quill.
  - `entityList`: riferimento a un'entità già scaffoldata, il cui elenco
    viene interpretato/renderizzato dall'app host.
  - `component`: riferimento generico a un componente esterno (tag + config
    JSON libera), pensato per essere risolto dall'app host (es. un componente
    Angular montato dinamicamente).

Il rendering front-end dei blocchi è sempre a carico dell'app host: il CMS si
limita a comporre ed editare la struttura JSON.

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

> Questo controllo copre lo spazio di URL che il CMS **conosce** (Pagine e
> Menu). Non copre invece URL "di dettaglio" per singoli record di **qualunque**
> tabella scaffoldata (non solo un caso specifico: il concetto è generale,
> vale per ogni entità): il CMS non ha oggi un concetto di routing per record
> — vedi la fase 15 di [`docs/ROADMAP.md`](docs/ROADMAP.md) per l'ipotesi di
> progettazione.

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

## Roadmap

Lo stato dettagliato di ogni fase (comprese le prossime: localizzazione
multi-lingua nel backoffice e ulteriori espansioni del modulo FFM) è
tracciato in [`docs/ROADMAP.md`](docs/ROADMAP.md).

