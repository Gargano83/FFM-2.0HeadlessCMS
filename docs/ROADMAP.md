# Roadmap di sviluppo — DAMIHeadlessCMS

CMS .NET 10, distribuito come libreria (Razor Class Library), integrabile in
un'applicazione MVC host. Approccio scelto per CRUD/scaffolding: **metadata-driven
runtime rendering** (nessuna generazione di file .cs/.cshtml, tutto pilotato da
metadati salvati nello schema `cms.*` del database).

## Fasi completate

- [x] **1. Core**: schema tabelle di sistema (`cms.EntityDefinition`,
      `cms.FieldDefinition`, `cms.Page`, `cms.Menu`, `cms.MenuItem`) + `CmsDbContext`
      con configurazioni Fluent API + prima migration (`InitialCmsSchema`).
- [x] **2. Scaffolding engine**: lettura schema DB SQL Server tramite query
      dirette su `sys.tables`/`sys.columns`/`sys.foreign_keys` (niente API
      interne EF) + `ScaffoldingService` che popola `EntityDefinition`/
      `FieldDefinition` con inferenza di `EditorType`, idempotente su
      riesecuzioni.
- [x] **3. Generic CRUD**: `IGenericEntityRepository`/`GenericEntityRepository`
      (SQL dinamico parametrico, whitelisting colonne dai metadati) +
      `GenericEntityController` con routing per `EntityDefinition.Id` (Guid) +
      wizard di scaffolding a stepper (`ScaffoldingWizardController` +
      `/dami/scaffolding`) + vista di sola lettura della struttura fisica
      (`/dami/{entityId}/structure`) con pulsante "Aggiorna struttura".
      Sezione **"Dati"** (CRUD sulle righe) e sezione **"Struttura/Scaffolding"**
      (mai editabile a livello di schema fisico, solo letta/ri-letta) sono
      nettamente separate nel backoffice, come da decisione architetturale.
- [x] **4. Identity**: ASP.NET Core Identity dedicato al backoffice (schema
      `cms.*`, tabelle rinominate senza prefisso `AspNet`), ruoli `CmsAdmin`/
      `CmsEditor`, policy di autorizzazione (`AdminPolicy`/`EditorPolicy`),
      login/logout, seeding del primo admin da configurazione, CRUD utenti
      riservato a `CmsAdmin`.
- [x] **5. Page builder + Menu**: `CmsPage` con contenuto a blocchi
      (html/entityList/component) editabile con SortableJS (drag&drop) e Quill
      (rich text); `CmsMenu`/`CmsMenuItem` con editor ad albero drag&drop,
      salvataggio "full replace" della struttura, e supporto a tre tipi di
      destinazione (`Page`, `Entity`, `ExternalUrl`).
- [x] **6. Editor avanzati**: `IFileStorageProvider` (default: filesystem locale
      su `wwwroot/uploads` dell'host) per `EditorType.File`; editor RichText
      (Quill) nel CRUD generico; autocomplete FK con ricerca live e debouncing
      via endpoint `/dami/lookup/{fieldId}`.
- [x] **6.1 Localizzazione legacy**: `LocalizationSource` per il pattern
      "a chiave condivisa" (es. `WN_LOCALIZZAZIONE`/`WN_LINGUE`, integrità solo
      applicativa, nessuna FK fisica). Lettura tramite subquery correlate,
      scrittura tramite transazioni ADO.NET esplicite che gestiscono sia la
      riga di traduzione sia la colonna "contenitore" nella tabella applicativa.
- [x] **6.2 Menu — apertura in nuova scheda**: campo esplicito
      `CmsMenuItem.OpenInNewTab` (bool), configurabile per singola voce di menu
      indipendentemente dal `TargetType`, editabile dalla UI ad albero del
      menu. Il CMS si limita a **generare e persistere l'alberatura** (incluso
      questo flag): il rendering HTML del menu — e quindi l'uso pratico del
      flag per produrre `target="_blank"` — resta responsabilità del progetto
      host, che consuma `CmsMenu`/`CmsMenuItem` per costruire la propria
      navigazione front-end.
- [x] **6.3 Fix di sicurezza — `.gitignore` disallineato dopo il rename**:
      dopo il rename `MyCms` → `DAMIHeadlessCMS`, il `.gitignore` continuava a
      escludere i vecchi percorsi (`src/MyCms.Data/appsettings.json`, ecc.),
      lasciando potenzialmente **non ignorati** i file `appsettings.json` reali
      (con credenziali del database) di `DAMIHeadlessCMS.Data` e
      `DAMIHeadlessCMS.TestHost`. Corretto aggiornando i percorsi. **Azione
      consigliata**: verificare con `git log --all --full-history -- <path>`
      se questi file sono già stati committati in passato; in caso affermativo,
      ruotare immediatamente la password del database e valutare una pulizia
      della cronologia Git (es. `git filter-repo`).
      **Aggiornamento (fase 13)**: verifica eseguita, i file risultavano
      effettivamente già committati — vedi fase 13 per l'esito completo e il
      pattern adottato per evitare che si ripeta.
- [x] **7. Modulo FFM — componenti Angular/Syncfusion dedicati (solo
      `CmsAdmin`)**: pagine di backoffice dedicate, riservate al ruolo
      `CmsAdmin`, che ospitano componenti Angular/Syncfusion, per tabelle
      `FFM.*` la cui UI non è riconducibile al CRUD generico metadata-driven.
      Modulo opzionale (`AddDAMIHeadlessCMSFfm(connectionString, defaultLanguageId)`),
      non parte del core del CMS.
  - [x] **7.1 Gestione `FFM.Giocatori`**:
        - Backend: `FfmGiocatoriRepository` (ADO.NET dedicato) +
          `FfmGiocatoriApiController` (`/dami/ffm/api/giocatori`, CRUD + import
          Excel) + `FfmController.Giocatori` (`/dami/ffm/giocatori`). Sostituisce
          integralmente il vecchio `SyncfusionController` legacy per questa parte.
        - Frontend: componente Angular aggiornato da Angular 15 a 17, Syncfusion
          EJ2 alla versione più recente (licenza community invariata),
          rifattorizzato da app Angular "intera" a **Custom Element**
          (`<dami-ffm-giocatori-grid>`, via `@angular/elements`) — nessuna
          dipendenza da `window.appSettings`, parametri via attributi HTML.
          Build dedicata via `ngx-build-plus` (configurazione `giocatori-widget`
          in `angular.json`), 4 asset statici copiati in
          `DAMIHeadlessCMS.Admin/wwwroot/ffm-widgets/giocatori/`. Chiave di
          licenza Syncfusion letta da configurazione server-side
          (`DAMIHeadlessCMS:Ffm:SyncfusionLicenseKey`), mai hardcoded.
        - ⚠️ **Comportamento preservato dal legacy**: l'import Excel sincronizza
          l'intero `FFM.Giocatori` con il file caricato, **eliminando i
          giocatori non presenti nel file**. Replicato identico per fedeltà
          funzionale, con conferma esplicita richiesta all'utente lato UI.
        - Formato a 3 decimali per `ValoreDiMercato`/`Stipendio`, sia in
          visualizzazione sia in editor (invece dei 2 decimali di default).
  - [x] **7.2 Gestione rosa squadra (`FFM.SquadreRelGiocatori`)**:
        - Architettura: il dato anagrafico di `FFM.Squadre` (presidente,
          allenatore, logo, nome localizzato, ecc.) è gestito dal **CRUD
          generico** una volta scaffoldata la tabella — nessun codice ad hoc
          duplicato per quella parte. Una nuova pagina indice leggera
          (`/dami/ffm/squadre`, `FfmController.Squadre`) elenca le squadre e
          linka sia la vista Edit generica (se scaffoldata) sia la pagina
          custom "Rosa". La gestione rosa (`/dami/ffm/squadre/{id}/rosa`,
          `FfmController.Rosa`) resta invece un modulo Angular/Syncfusion
          dedicato, perché la UI (grid con riga di dettaglio espandibile,
          autocomplete giocatori svincolati, aggregati finanziari) non è
          riconducibile agli `EditorType` standard.
        - Backend: `FfmSquadraRepository` (ADO.NET dedicato — InfoSquadra
          aggregata con conteggi Tesserati/InRosa/ListaA/Under22 per stagione
          attiva, rosa, dettaglio, giocatori svincolati, aggiungi/rimuovi/
          aggiorna giocatore in rosa) + `FfmSquadreApiController`
          (`/dami/ffm/api/squadre/*`). Riusa **as-is** la funzione SQL legacy
          `dbo.udf_Localize` per risolvere `FFM.Squadre.Nome` (nessuna
          reimplementazione della logica di localizzazione), tramite l'id
          lingua di default passato a `AddDAMIHeadlessCMSFfm`.
        - **Mapping utente per `FFM.SquadreRelGiocatori.IdUtente`**:
          `IFfmUserResolver`/`FfmUserResolver` risolve l'utente CMS loggato
          (`CmsUser.Email`) all'Id utente legacy corrispondente in
          `dbo.WN_UTENTI` (corrispondenza 1:1 via `UT_Email` → `UT_ID`,
          confermata dall'utente del progetto). Se non c'è corrispondenza,
          `IdUtente` viene scritto `NULL` piuttosto che bloccare l'operazione
          con un utente CMS non ancora mappato in `WN_UTENTI`.
        - Frontend: nuovo componente `SquadraRosterComponent`
          (`<dami-ffm-squadra-roster idsquadra="...">`), con entry point
          Angular dedicato (`main-squadra.ts`) e build dedicata
          (configurazione `squadra-widget`, bundle separato in
          `wwwroot/ffm-widgets/squadra/`). Il vecchio popup Angular Material
          (`ModalComponent`) per il dettaglio giocatore-squadra è stato
          **sostituito** da una riga di dettaglio espandibile della Grid
          stessa (`detailTemplate` di Syncfusion Grid): stesso risultato
          funzionale (visualizza/modifica Stato e Mesi), una dipendenza in
          meno da mantenere.
        - `AbilitaModifica` (flag letto da `FFM.Squadre`) è esposto nel DTO
          `InfoSquadra` così com'è: governa l'area riservata front-end
          (fuori dal perimetro del CMS), non limita le azioni nel backoffice,
          dove l'accesso è già filtrato da `CmsAdmin`.
  - Nota architetturale: entrambe le pagine (7.1, 7.2) **non** passano dal CRUD
    generico metadata-driven (`GenericEntityController`) per le parti a UI
    specifica — solo `FFM.Squadre` "anagrafica" lo usa, come da punto 7.2.
- [x] **8. Voce di menu verso il "Regolamento" (Docusaurus)**: **nessuna
      integrazione di rendering è richiesta lato CMS**. Il sito compilato con
      Docusaurus (che oggi sul progetto legacy produce la pagina
      "regolamento") verrà pubblicato come asset statico dal **progetto host**
      (non da DAMIHeadlessCMS), tipicamente sotto una cartella tipo
      `wwwroot/regolamento` servita da `UseStaticFiles()`. Il CMS deve solo
      permettere di creare, nell'editor ad albero del menu, una voce con
      `TargetType = ExternalUrl`, `TargetValue = "/regolamento"` (o il percorso
      scelto) e `OpenInNewTab = true`, cosa già coperta dalla fase 6.2. Nessun
      ulteriore sviluppo è quindi pianificato su questo punto, se non — in
      caso di necessità emerse in corso d'opera — piccoli aggiustamenti alla
      validazione del campo URL nel form del menu.

- [x] **9. Ruolo intermedio `CmsOperator`**: terzo ruolo tra `CmsAdmin` ed
      `CmsEditor`. Lettura/scrittura piena su Dati/Pagine/Menu (stessa policy
      `EditorPolicy` di `CmsEditor`, ora estesa anche a `CmsOperator`); sola
      lettura — nessuna operazione di scrittura — su Struttura (vista
      `/dami/{id}/structure`, non lo scaffolding/wizard, riservato a
      `CmsAdmin`), Utenti, Localizzazioni e pagine dedicate del modulo FFM
      (Database Giocatori, Squadre/Rosa). Quattro nuove policy dedicate
      (`StructureViewPolicy`, `UsersViewPolicy`, `LocalizationViewPolicy`,
      `FfmViewPolicy`, tutte `CmsAdmin` **o** `CmsOperator`), applicate a
      livello di controller/azione insieme a `AdminPolicy` sulle sole azioni
      di scrittura (pattern già usato per Struttura/Scaffolding fin dalla
      fase 3). Nelle view MVC coinvolte i controlli di scrittura sono
      nascosti/disabilitati in base a `User.IsInRole(CmsRoles.Admin)`; nei
      Custom Element Angular del modulo FFM un nuovo attributo HTML
      `read-only` (calcolato server-side dalla stessa view) disattiva editing,
      toolbar e comandi della Grid — l'enforcement reale resta comunque lato
      server sulle rispettive API REST, la UI è solo di supporto.
      **9.1 Seeding automatico esteso**: `DAMIHeadlessCMSIdentitySeeder`
      generalizzato con un elenco di `SeedUserSpec` (ruolo + chiave di
      configurazione), stesso pattern già usato per `SeedAdmin`, ora applicato
      anche a `SeedEditor`/`SeedOperator` in `appsettings` — ogni blocco è
      indipendente e facoltativo.
- [x] **11. Riordino sezioni sidebar**: ordine definitivo del menu laterale —
      **Amministrazione → Struttura → FFM → Contenuti → Dati** (in precedenza
      era Struttura, Contenuti, Dati, FFM, Amministrazione). Nessuna modifica
      di logica: solo riposizionamento dei blocchi `<div class="cms-nav-section">`
      in `_Layout.cshtml`, le condizioni di visibilità per ruolo (Amministrazione
      e FFM visibili a `CmsAdmin`/`CmsOperator`, Scaffolding solo a `CmsAdmin`)
      restano quelle introdotte in fase 9.
- [x] **12. Unicità URL nella creazione del menu**: introdotto
      `InternalUrlPath` (`DAMIHeadlessCMS.Admin.Utilities`), helper condiviso
      che normalizza e confronta i **percorsi interni** del sito — quelli che
      iniziano con `/` ma non sono protocol-relative (`//host/...`): i link
      davvero esterni (`http://`, `https://`, `mailto:`, ecc.) sono esclusi dal
      controllo perché non competono per lo spazio di URL del CMS.
  - Le voci di menu `TargetType = Page` erano già "al sicuro" perché lo slug
    si sceglie da una dropdown alimentata da `CmsPage.Slug`, già validato
    **globalmente unico** (non solo tra fratelli) fin dalla fase 5 — quindi
    anche un URL costruito annidando la gerarchia `CmsPage.ParentId`
    (`/livello1/livello2/pagina`) non può mai collidere, perché lo slug finale
    è comunque unico su tutto il sito.
  - Il vero gap era sulle voci `TargetType = ExternalUrl` (testo libero, es.
    `/regolamento`), prive di qualunque validazione. `MenusController.Save`
    ora rifiuta (HTTP 400 con messaggio, mostrato dalla UI ad albero del menu)
    un salvataggio se un percorso interno `ExternalUrl`: è duplicato
    all'interno dello stesso salvataggio; collide con un `ExternalUrl` di un
    **altro** menu; oppure collide con lo slug di una `CmsPage` esistente (in
    quel caso la voce corretta è di tipo "Pagina", non "URL esterno").
    Speculare: `PagesController` ora rifiuta la creazione/modifica di una
    `CmsPage` il cui slug collide con un `ExternalUrl` interno già configurato
    in un menu.
  - Aggiunto anche un controllo anti-ciclo sulla gerarchia `CmsPage.ParentId`
    in `PagesController.Edit` (una pagina non può diventare discendente di una
    propria sotto-pagina), a protezione della coerenza dell'albero — difetto
    latente scoperto analizzando il caso d'uso, non il cuore della richiesta.
  - **Nota di progettazione (non implementata in questa fase)**: il caso
    d'uso — routing di dettaglio per singoli record di una tabella scaffoldata
    — è stato generalizzato e promosso a fase a sé stante, vedi **fase 15**
    più sotto.
- [x] **13. `appsettings` per ambiente + pulizia repository**:
  - **Incidente rilevato**: `git log --all --full-history` ha confermato che
    sia `src/DAMIHeadlessCMS.TestHost/appsettings.json` sia
    `src/DAMIHeadlessCMS.Data/appsettings.json` erano stati committati più
    volte (l'ultima il giorno stesso della fase 9), **nonostante fossero già
    elencati in `.gitignore`** — perché `.gitignore` impedisce di aggiungere
    un file mai tracciato, ma non smette di tracciarne uno già presente
    nell'indice: serve un esplicito `git rm --cached`. Contenevano credenziali
    reali (password SQL Server, email/password dell'utente admin di seed,
    chiave di licenza Syncfusion). **Raccomandazione**: ruotare al più presto
    queste credenziali, indipendentemente da un'eventuale pulizia della
    cronologia Git (che rimuove la visibilità futura, non annulla
    un'esposizione già avvenuta).
  - **Pattern adottato** (due file per progetto, non un file per ogni
    ambiente — sufficiente per le esigenze attuali): `appsettings.json`
    **versionato**, con solo placeholder (`CAMBIAMI`, blocchi `SeedAdmin`/
    `SeedEditor`/`SeedOperator`/`SyncfusionLicenseKey` vuoti — il seeding resta
    disattivato finché non configurato esplicitamente) +
    `appsettings.Development.json` **locale, mai committato**, con i valori
    reali. Applicato a entrambi i progetti con file di configurazione:
    - `DAMIHeadlessCMS.TestHost`: nessuna modifica di codice necessaria,
      `WebApplication.CreateBuilder` fa già il layering `appsettings.json` +
      `appsettings.{ASPNETCORE_ENVIRONMENT}.json` in automatico, e
      `launchSettings.json` imposta già `Development` su tutti i profili.
    - `DAMIHeadlessCMS.Data` (libreria senza `Program.cs`, la connection
      string per `dotnet ef` viene letta da `CmsDbContextFactory`): il
      **vecchio file `appsettings.example.json` è stato eliminato** e il suo
      contenuto (già solo placeholder) è diventato il nuovo
      `appsettings.json` versionato. `CmsDbContextFactory` è stata estesa per
      fare lo stesso layering a due file dell'host, leggendo
      `DOTNET_ENVIRONMENT`/`ASPNETCORE_ENVIRONMENT` (default `Development` se
      nessuna delle due è impostata, coerente con l'uso da riga di comando di
      `dotnet ef`).
  - **Comandi Git da eseguire manualmente** (rimuovono dall'indice le versioni
    con segreti, così i nuovi file placeholder possano essere ri-aggiunti e
    committati normalmente — non riscrivono la cronologia esistente):
    ```
    git rm --cached src/DAMIHeadlessCMS.TestHost/appsettings.json
    git rm --cached src/DAMIHeadlessCMS.Data/appsettings.json
    git rm --cached src/DAMIHeadlessCMS.Data/appsettings.example.json
    ```
    seguiti dall'applicazione dello zip di questa fase e da un normale
    `git add`/commit. La pulizia della cronologia pregressa (`git filter-repo`
    o BFG Repo-Cleaner + force-push) resta una scelta a parte, deliberata e
    non urgente quanto la rotazione delle credenziali.
- [x] **14. Dashboard post-login arricchita**: la pagina che segue il login
      (`/dami`, `GenericEntityController.Index`) mostra ora, oltre all'elenco
      "Entità gestite" già presente dalla fase 3:
  - **Contatori riepilogativi**: entità scaffoldate, pagine (totali e
    pubblicate), voci di menu, utenti per ruolo — letti con query dirette
    (nessuna nuova tabella per questi).
  - **Log di audit** (nuova tabella `cms.AuditLogEntry`): traccia
    automaticamente le operazioni di creazione/modifica/eliminazione sulle
    entità CMS-native — `CmsPage`, `CmsMenu`, `CmsMenuItem`, `CmsUser`.
    Generato interamente da un override di `CmsDbContext.SaveChangesAsync`
    che legge il `ChangeTracker` di EF Core prima di salvare: nessun
    controller scrive esplicitamente una riga di audit, funziona anche per le
    scritture fatte tramite `UserManager`/`RoleManager` (stesso
    `CmsDbContext`). L'utente corrente si ottiene da `IHttpContextAccessor`
    (nuova registrazione `AddHttpContextAccessor()`), `null` fuori da una
    richiesta HTTP (seeding, `dotnet ef`) — atteso, non un errore.
    **Scope deliberatamente limitato**, come discusso in fase di
    elicitazione dei requisiti: solo entità EF-native, **non** le tabelle
    applicative scaffoldate (sezione "Dati", lette/scritte via ADO.NET fuori
    dal ChangeTracker — richiederebbe un meccanismo separato, rimandato) né
    le tabelle di supporto Identity (ruoli assegnati/claim/token, per non fare
    rumore né loggare dati sensibili). Le voci `CmsUser` e i contatori per
    ruolo sono visibili solo a `CmsAdmin`/`CmsOperator`, coerentemente con
    `UsersViewPolicy` (fase 9) — un `CmsEditor` non vede di riflesso, qui,
    informazioni su un'area (Utenti) a cui non ha accesso diretto.
  - **Pagine recenti**: le ultime `CmsPage` create/modificate, con link
    diretto alla modifica.
  - Nota tecnica: `MenusController.Save` (strategia "full-replace") genera
    più righe di audit per singolo salvataggio dell'albero (una per voce
    eliminata/creata), non un singolo "Update" — riflette accuratamente
    l'implementazione del salvataggio, non è un difetto.

- [x] **15. Routing di dettaglio per record di entità scaffoldate**: implementata
      esattamente secondo l'ipotesi di progettazione fissata in questa fase —
      concetto **generalizzato** a qualunque tabella scaffoldata (l'esempio
      `WN_CATEGORIE` → `WN_DOCUMENTI` discusso in fase 12 era solo un caso
      concreto che aveva fatto emergere il bisogno, non lo scope reale).
  - **`EntityDefinition`**: due nuove proprietà opzionali, `DetailRoutePrefix`
    (percorso interno, es. `/categorie`, max 200 caratteri) e `DetailKeyFieldId`
    (FK opzionale a `FieldDefinition`, `OnDelete(SetNull)` — non `Cascade`,
    per non creare un secondo percorso di cascata tra le stesse due tabelle
    nella direzione opposta a `FieldDefinition.EntityDefinitionId`, che
    avrebbe causato l'errore SQL Server "may cause cycles or multiple cascade
    paths"). Se `DetailKeyFieldId` è null ma `DetailRoutePrefix` è valorizzato,
    convenzione: fallback sulla chiave primaria. L'URL di un record è
    `{DetailRoutePrefix}/{valore(DetailKeyField ?? chiave primaria)}`.
  - **Unicità dello spazio di URL** (statica, a configurazione salvata):
    doppio livello di difesa.
    - **Applicativo** (`ScaffoldingWizardController.ValidateDetailRoutePrefixesAsync`,
      eseguito prima di persistere): `DetailRoutePrefix` dev'essere un
      percorso interno valido (`InternalUrlPath.IsInternal`, stesso registro
      introdotto in fase 12) e univoco — né duplicato tra le entità dello
      stesso salvataggio, né in conflitto con un'altra `EntityDefinition` già
      configurata, né con lo slug di una `CmsPage`, né con un percorso interno
      `ExternalUrl` di menu. In caso di conflitto: HTTP 400 con messaggio,
      mostrato dal wizard (stesso pattern di `MenusController.Save`).
    - **Database** (`EntityDefinitionConfiguration`): indice univoco
      **filtrato** su `DetailRoutePrefix` (`WHERE [DetailRoutePrefix] IS NOT
      NULL`) — complementare, non sostitutivo, al controllo applicativo: la
      tabella `EntityDefinition` da sola non può sapere di `CmsPage`/`CmsMenuItem`.
  - **Unicità dei valori a runtime** (dinamica, per singolo record):
      confermata la scelta di non imporla — resta responsabilità dei dati
      stessi (tabella legacy sottostante), coerente con "integrità solo
      applicativa" già adottato per le tabelle FFM/legacy. Il CMS non altera
      lo schema fisico delle tabelle applicative scaffoldate.
  - **UI**: due nuovi campi nella card di ogni entità nel wizard di
    scaffolding (`/dami/scaffolding`) — prefisso (testo libero) e campo
    chiave (select sulle colonne della tabella, "usa la chiave primaria" come
    default). Il campo chiave viaggia nel payload come **nome colonna**, non
    come `FieldDefinition.Id`: per una tabella appena selezionata i
    `FieldDefinition` reali non esistono ancora al momento della
    compilazione del payload (vengono creati da `ScaffoldTablesAsync` durante
    lo stesso salvataggio), quindi la risoluzione a `Guid` avviene
    server-side, dopo lo scaffold. La vista Struttura (`/dami/{id}/structure`)
    mostra il routing configurato, se presente.
  - **Routing runtime**: resta, come previsto, responsabilità del progetto
    host — il CMS genera/valida i metadati (prefisso + campo chiave,
    garantendone l'unicità), non gestisce il matching URL → record a runtime,
    coerentemente con l'architettura "il CMS genera/valida i metadati, l'host
    renderizza".

- [x] **16. Migrazione pagine pubbliche legacy → `DAMIHeadlessCMS.TestHost`**
      (in corso, per checkpoint — vedi PDF "Indicazioni utili per migrazione
      progetto legacy" per l'elenco pagine/endpoint di riferimento). Non è una
      feature della libreria: `TestHost` simula l'app host definitiva, quindi
      questa fase **non** aggiorna il `README.md` della libreria.
  - **Decisione chiave**: i contenuti legacy (`WN_Contenuti`, `FFM.Squadre`,
    `FFM.RiepilogoStatistiche`, ecc.) **non vengono ricreati a mano** nel
    nuovo CMS — si scaffoldano da `/dami/scaffolding` e si leggono da
    `TestHost` tramite `IGenericEntityRepository` (stesso repository usato
    dal CRUD generico di backoffice), che risolve già in automatico i campi
    localizzati via `LocalizationSource` (subquery equivalente a
    `dbo.udf_Localize`/`WN_LOCALIZZAZIONE`) — nessuna modifica alla libreria
    necessaria per questo. Introdotto `LegacyContentReader`
    (`DAMIHeadlessCMS.TestHost/PublicSite`), wrapper sottile sopra
    `IGenericEntityRepository` per il solo TestHost.
  - **Menu**: sostituisce l'endpoint legacy `/api/data/menu` (che nel
    progetto legacy usava un albero per stringa `ca_ordine`, con nota
    esplicita "va ripensata in base al CMS") con `CmsMenu`/`CmsMenuItem`
    nativi — albero vero via `ParentId`, non da ricostruire da `WN_Categorie`.
    Nome del menu principale: convenzione fissa nel codice
    (`MainMenuViewComponent.MainMenuName = "main-nav"`), non configurabile.
  - **Routing**: `/` → `HomeController` (pagine pubbliche del TestHost);
    `/{slug}` → `PagesController.Show`, rotta convenzionale generica per le
    `CmsPage` native (contenuto **nuovo**, non legacy — interpreta solo
    blocchi `{"type":"html"}` di `ContentJson` per ora); `/dami/*` resta
    sull'attribute routing esposto dalla Razor Class Library Admin, non
    impattato.
  - **Checkpoint 1/4 — Menu + Hero** ✅: layout pubblico condiviso
    (`_PublicLayout.cshtml`, Bootstrap 5.3.3 via CDN, stessa versione del
    backoffice), `MainMenuViewComponent` (navbar responsive con sottomenu
    annidati), `HomeController.Index` che legge l'hero da `WN_Contenuti`
    (colonne `co_titolo`/`co_abstract`/`co_corpo`, tutte localizzate — via
    `Doc.cs`/`Default.xml` legacy, query `GetDocumento`) per l'id configurato
    in `PublicSite:HomepageDocumentId` (=1, equivalente di
    `WebConst.HOMEPAGE_ID`). Aggiunta anche l'azione `Error` standard, prima
    assente (referenziata da `UseExceptionHandler` in `Program.cs` ma
    orfana finché non esisteva un `HomeController`).
    - **Bug scoperto in fase di test** (non introdotto da questo checkpoint,
      ma emerso qui perché è il primo punto che interroga `CmsMenuItem` con
      EF): la migration `20260705092847_AddOpenInNewTabToMenuItem` aveva
      `Up()`/`Down()` vuoti — la colonna non veniva creata da nessuna parte,
      nemmeno su un DB nuovo. Contenuto ripristinato nel repository; su
      database già migrati va applicato a mano un `ALTER TABLE` una tantum
      (non recuperabile da `dotnet ef database update`, la cronologia la
      segna già come applicata).
  - **Checkpoint 2/4 — Squadre** ✅: slider loghi squadre
    (`FFM.Squadre`, nessun filtro — letto con `LegacyContentReader.GetAllRowsAsync`,
    wrapper su `GetListAsync` con una sola pagina "grande", nessuna modifica
    alla libreria) con [swiffy-slider](https://swiffyslider.com) via CDN
    (stessa libreria usata dal client-side legacy). Url dei loghi composta da
    `PublicSite:LegacyFileBaseUrl` + colonna `LogoStatistiche` (path
    relativo) — **valore da confermare**, al momento impostato a titolo
    indicativo in `appsettings.json`.
  - **Checkpoint 3/4 — Ultimi articoli comunicazioni** ✅: **estesa la
    libreria** — nuovo `IGenericEntityRepository.QueryAsync(entity, filters,
    sort, top)` (+ tipi ausiliari `QueryFilter`/`QueryFilterOperator`/
    `QuerySort` in `DAMIHeadlessCMS.Admin.Data`), filtro/ordinamento solo su
    colonne NON localizzate (lancia `InvalidOperationException` altrimenti —
    filtrare/ordinare sul testo tradotto di una colonna localizzata
    richiederebbe una semantica diversa, non supportata). Riusa
    `BuildSelectExpression`/whitelisting esistenti, stesso approccio di
    `GetListAsync`/`GetByIdAsync`. `LegacyContentReader.GetFilteredRowsAsync`
    (TestHost) ne è il wrapper. `HomeController.LoadLatestArticlesAsync`:
    `WN_Contenuti` filtrata per `co_tipo_doc`/`co_attivo`, ordinata per
    `co_data_inizio DESC, co_id DESC`, `TOP 6`; nome categoria risolto **a
    parte, riga per riga** (`WN_Categorie` via `GetRowByIdAsync`, che risolve
    già la localizzazione di `ca_nome` — scelta deliberata: N+1 accettabile
    per 6 righe, evita di dover fare join dinamici in `QueryAsync`).
    **Richiede configurazione**: `PublicSite:ArticleDocTypeId` (valore di
    `WebConst.COMUNICAZIONE_ARTICOLO_TPD` nel legacy) non è ancora
    valorizzato — da confermare prima del test.
  - **Checkpoint 4/4 — Riepilogo statistiche "Albo d'oro"** ✅:
    `FFM.RiepilogoStatistiche` (`Id`, `Stagione`/`Competizione` FK a
    `WN_LOOKUP`, `Squadra` FK a `FFM.Squadre`) letta senza filtro
    (`GetAllRowsAsync`), pivotata dinamicamente in `HomeController.LoadHallOfFameAsync`:
    righe = stagioni trovate nei dati, colonne = competizioni trovate nei
    dati (nessun id hardcoded), entrambe ordinate per `LK_ORDINE` di
    `WN_LOOKUP` (stesso criterio della query legacy, applicato dinamicamente
    invece che con colonne fisse). Etichette risolte con cache in-memory per
    id già visti (stagioni/competizioni via `WN_LOOKUP.LK_Valore`, squadre
    via `FFM.Squadre.Nome`, già disponibile dal checkpoint 2) — evita
    risoluzioni ripetute per lo stesso id su più righe.
    - **Correzione**: le istruzioni originali dicevano di NON marcare
      `WN_LOOKUP.LK_Valore` come localizzato — errato, è localizzato come
      tutto il resto (stesso pattern `udf_Localize`/`WN_LOCALIZZAZIONE`).
      Scoperto in fase 17 lavorando sul riferimento manuale FK. Da
      correggere nello scaffold di `WN_LOOKUP` (marcare `LK_Valore` come
      Localizzato) perché anche questo blocco mostri le etichette invece
      degli id grezzi.

**Fase 16 completata** (tutti i 4 checkpoint della Homepage). Prossima
pagina da migrare: **Statistiche** (vedi PDF di riferimento), che riusa
`/api/data/menu` (già coperto) e introduce `/api/statistiche/campionato` +
gli altri endpoint statistiche non ancora affrontati.

- [x] **17. Wizard di scaffolding: riferimento manuale a un'altra tabella
      (FK senza vincolo fisico), con filtri**. Scoperto durante il test del
      Checkpoint 4 della fase 16: `FFM.RiepilogoStatistiche.Stagione` e
      `.Competizione` puntano a `WN_LOOKUP`, ma filtrate per `LK_LG_ID`
      diverso (1 e 32) — nessun vincolo FK fisico, e la stessa tabella serve
      più campi con sottoinsiemi diversi. Il wizard oggi rilevava FK solo da
      vincoli fisici reali: nessun modo di dichiararle manualmente.
  - **Libreria** (`DAMIHeadlessCMS.Core`/`Admin`/`Scaffolding`): nuova
    `FieldDefinition.ForeignKeyFiltersJson` (JSON, array di condizioni
    `{ColumnName, Operator, Value}`, tutte in AND); `IGenericEntityRepository
    .GetLookupOptionsAsync` esteso con un parametro `filters` opzionale
    (`ForeignKeyFilterCondition`, stessi 6 operatori di `QueryFilterOperator`
    già introdotto in fase 16 checkpoint 3); `ScaffoldingService` espone
    `GetTableColumnsAsync` (colonne di una tabella qualsiasi, scaffoldata o
    meno) e `PreviewAsync` ora riporta anche l'eventuale riferimento manuale
    già salvato in precedenza (per precompilare il wizard).
  - **Tabella di destinazione scelta anche non ancora scaffoldata**: se un
    campo referenzia una tabella non ancora mappata, `ScaffoldingWizardController
    .Save` la include automaticamente nello stesso batch passato a
    `ScaffoldTablesAsync` (scaffold "a cascata", impostazioni di default —
    rifinibile poi manualmente).
  - **Non tocca mai una FK fisica già rilevata**: il riferimento manuale si
    applica solo se esplicitamente configurato per quel campo in questo
    salvataggio.
  - **Wizard** (`Views/ScaffoldingWizard/Index.cshtml`): nuova colonna
    "Riferimento" per campo, riga espandibile con select tabella (raggruppata
    per schema, tutte le tabelle del DB) + select colonna etichetta
    (popolata via AJAX su `/dami/scaffolding/columns`) + elenco filtri
    aggiungibili/rimovibili (colonna/operatore/valore).
  - **Bug scoperto in fase di test**: la lista "Dati" mostrava il valore
    grezzo (l'id) anche per campi FK correttamente configurati — comportamento
    preesistente per **qualsiasi** FK (fisica o manuale), non legato a questa
    feature: `GetListAsync`/`BuildSelectExpression` risolveva in lettura solo
    i campi localizzati, mai le FK (che venivano risolte solo lato form di
    editing, tramite l'autocomplete AJAX). Esteso `BuildSelectExpression` con
    lo stesso approccio a subquery già usato per la localizzazione, attivo
    **solo** per `GetListAsync` (parametro `resolveForeignKeys`) — `GetByIdAsync`/
    `QueryAsync` restano invariati e continuano a restituire il valore grezzo,
    necessario al form di editing per legare l'input al valore reale.
  - **Secondo bug scoperto in fase di test**: anche dopo il fix sopra, i
    campi FK mostravano ancora il valore grezzo quando la colonna scelta
    come etichetta è a sua volta localizzata (es. `WN_LOOKUP.LK_Valore`,
    `FFM.Squadre.Nome`: entrambe seguono il pattern `udf_Localize` — non è
    un caso isolato, è la norma per le colonne di testo delle tabelle
    legacy). Serve un doppio salto: FK → riga di destinazione → colonna
    etichetta → (se localizzata) `WN_LOCALIZZAZIONE`. Aggiunto
    `GenericEntityRepository.BuildForeignKeyLabelSource`, helper condiviso
    da `BuildSelectExpression` (lista Dati), `GetLookupOptionsAsync` e
    `GetLookupLabelAsync` (autocomplete) — prima quest'ultime due leggevano
    la colonna etichetta senza risolverne la localizzazione, quindi
    ricerca/ordinamento nell'autocomplete erano silenziosamente rotti per
    qualunque FK con etichetta localizzata (confronto testo digitato contro
    id grezzo). Estesi anche gli `Include` di `LoadEntityAsync`/`Lookup`/
    `LookupLabel` per caricare i campi (e le rispettive `LocalizationSource`)
    dell'entità di destinazione, necessari per la risoluzione.
  - **Terzo bug scoperto in fase di test (regressione)**: il fix del punto
    precedente rendeva `GetListAsync` sempre risolutivo delle FK, senza
    possibilità di scegliere — questo ha rotto silenziosamente il blocco
    "Albo d'oro" della Homepage pubblica (fase 16, checkpoint 4):
    `LegacyContentReader.GetAllRowsAsync` (wrapper su `GetListAsync`, usato
    anche da `LoadHallOfFameAsync`) si aspettava id grezzi da convertire a
    `int`, e invece riceveva le etichette già risolte (stringhe) per
    `Stagione`/`Competizione`/`Squadra` — la conversione falliva
    silenziosamente (`as int? ?? 0`), tutte le righe venivano scartate, il
    blocco spariva senza errori visibili. **Fix**: `GetListAsync` ha ora un
    parametro esplicito `resolveForeignKeys` (default `false`, per non
    sorprendere i consumatori esterni che si aspettano il valore grezzo);
    solo `GenericEntityController.List` (griglia Dati del backoffice) lo
    passa `true`. Aggiornati tutti i punti di chiamata coerentemente.
  - **Migration**: solo modifica al modello (`ForeignKeyFiltersJson` su
    `FieldDefinition`) consegnata; migration EF generata/applicata in locale
    da Alessio (`dotnet ef migrations add`), come da prassi.

## Prossime fasi

- [ ] **10. Localizzazione multi-lingua nel backoffice**: attualmente il CMS
      legge/scrive sempre e solo nella lingua di default configurata (sia per
      `LocalizationSource` generiche sia per il modulo FFM, dove l'id lingua è
      un parametro fisso passato a `AddDAMIHeadlessCMSFfm`). Un selettore
      lingua nel backoffice (per editor multi-lingua sui campi localizzati)
      resta fuori dal perimetro attuale, deferita a quando servirà davvero.
- [ ] Ulteriori espansioni del modulo FFM o altre tabelle applicative, da
      valutare in base alle esigenze che emergeranno.

## Decisioni architetturali chiave

| Aspetto | Scelta |
|---|---|
| Packaging | Razor Class Library (NuGet), montata via routing MVC nell'app host |
| DB target | SQL Server |
| Generazione CRUD | Metadata-driven a runtime (`cms.EntityDefinition`/`cms.FieldDefinition`), nessuna ricompilazione richiesta |
| Lettura schema DB | Query dirette su `sys.tables`/`sys.columns`/`sys.foreign_keys` (no API interne EF) |
| Autenticazione | ASP.NET Core Identity dedicato al backoffice, ruoli `CmsAdmin`/`CmsOperator`/`CmsEditor` |
| Contenuti pagine custom | JSON a blocchi, ogni blocco può referenziare un componente esterno |
| Localizzazione legacy | Pattern "a chiave condivisa" descritto da `LocalizationSource` (generico) o riuso diretto di `dbo.udf_Localize` (modulo FFM), nessuna FK fisica |
| Componenti Angular ad hoc (fase 7) | Pagine di backoffice dedicate, fuori dal CRUD generico, riservate a `CmsAdmin`; un Custom Element + bundle `ngx-build-plus` dedicato per componente |
| Contenuti statici esterni (fase 8) | Serviti dal progetto host, il CMS espone solo il link nel menu |
| Mapping utenti legacy (modulo FFM) | Risoluzione via email (`IFfmUserResolver`) verso tabelle utenti legacy quando serve tracciare `IdUtente` su tabelle applicative esistenti |
| Log di audit (fase 14) | Generato da un override di `CmsDbContext.SaveChangesAsync` sul `ChangeTracker` di EF Core, non da scritture esplicite nei controller; copre solo le entità EF-native (Pagine/Menu/Utenti), non i dati scaffoldati (ADO.NET, fuori dal ChangeTracker) |
| Routing di dettaglio per record (fase 15) | Il CMS valida/conserva solo prefisso + campo chiave (`EntityDefinition`), doppio controllo di unicità (applicativo + indice DB filtrato); il matching URL → record a runtime resta al progetto host, come per pagine e menu |
