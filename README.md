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
   (`CmsAdmin` / `CmsEditor`), indipendente da un eventuale sistema di
   autenticazione già presente nel progetto host per gli utenti finali.

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

// Crea i ruoli CmsAdmin/CmsEditor e, se configurato, il primo utente admin.
await DAMIHeadlessCMSIdentitySeeder.SeedAsync(app.Services, app.Configuration);

app.Run();
```

Il backoffice risponde per default sotto il prefisso di route **`/dami`**
(login: `/dami/account/login`). Per il seed del primo amministratore,
in `appsettings`:

```json
{
  "DAMIHeadlessCMS": {
    "SeedAdmin": { "Email": "admin@example.com", "Password": "Pa$$w0rd1" }
  }
}
```

## Funzionalità implementate

### 1. Scaffolding — mappare le tabelle del database

Percorso backoffice: **Struttura → Scaffolding** (`/dami/scaffolding`,
riservato a `CmsAdmin`).

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

Due ruoli:

- **`CmsAdmin`**: accesso completo — dati, struttura/scaffolding,
  localizzazioni, gestione utenti.
- **`CmsEditor`**: solo CRUD sui dati delle entità già scaffoldate (niente
  struttura, niente gestione utenti).

Login/logout dedicati (`/dami/account/login`), cookie di autenticazione
proprio (`DAMIHeadlessCMS.Auth`), seeding del primo admin da configurazione al
primo avvio (indispensabile: senza un admin pre-esistente nessuno potrebbe
accedere al backoffice per crearne uno).

### 4. Pagine custom a blocchi

Percorso backoffice: **Contenuti → Pagine** (`/dami/pages`).

`CmsPage` rappresenta contenuti editoriali non legati a una tabella
applicativa (es. "Chi siamo"), con:

- Slug univoco, titolo, pagina genitore (struttura ad albero), stato
  pubblicato/bozza, ordinamento.
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
riservato a `CmsAdmin`).

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
- Il `DefaultLanguageId` di una `LocalizationSource` — l'unico "numero" mai
  incorporato direttamente nel testo SQL anziché come parametro — è un
  metadato configurato solo da `CmsAdmin`, non input utente a runtime.

## Roadmap

Lo stato dettagliato di ogni fase (comprese le prossime: pagine di
amministrazione con componenti Angular/Syncfusion dedicati per
`FFM.Giocatori` e `FFM.SquadreRelGiocatori`, e la voce di menu verso il
"Regolamento" pubblicato dal progetto host) è tracciato in
[`docs/ROADMAP.md`](docs/ROADMAP.md).
