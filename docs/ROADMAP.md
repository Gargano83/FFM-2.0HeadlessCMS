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

## Prossime fasi

- [ ] **10. Localizzazione multi-lingua nel backoffice**: attualmente il CMS
      legge/scrive sempre e solo nella lingua di default configurata (sia per
      `LocalizationSource` generiche sia per il modulo FFM, dove l'id lingua è
      un parametro fisso passato a `AddDAMIHeadlessCMSFfm`). Un selettore
      lingua nel backoffice (per editor multi-lingua sui campi localizzati)
      resta fuori dal perimetro attuale, deferita a quando servirà davvero.
- [ ] **14. Dashboard post-login arricchita**: la pagina che segue il login
      (oggi il solo elenco "Entità gestite") arricchita con funzionalità utili
      al backoffice — da valutare insieme (es. tracciamento attività
      dell'utente loggato, contatori/riepiloghi sulle entità gestite, ultime
      modifiche).
- [ ] **15. Routing di dettaglio per record di entità scaffoldate**: concetto
      **generalizzato** a qualunque tabella scaffoldata (l'esempio
      `WN_CATEGORIE` → `WN_DOCUMENTI` discusso in fase 12 era solo un caso
      concreto, non lo scope reale). Oggi il menu può puntare solo a una
      `CmsPage`, a un intero listato `Entity` (tutta la tabella), o a un
      `ExternalUrl` — non esiste alcun concetto di "URL del singolo record N
      della tabella X", per qualunque entità già scaffoldata (FFM.Squadre,
      WN_CATEGORIE o una qualsiasi tabella futura).
      Ipotesi di progettazione, da rifinire quando il bisogno diventerà
      concreto:
      - Nuove proprietà opzionali su `EntityDefinition`: `DetailRoutePrefix`
        (percorso interno, es. `/categorie`) e `DetailKeyFieldId` (FK a
        `FieldDefinition`: la colonna che fornisce il segmento URL del singolo
        record, es. uno `Slug` dedicato, oppure la PK come fallback). L'URL
        di un record diventa `{DetailRoutePrefix}/{valore(DetailKeyField)}`.
      - **Unicità dello spazio di URL** (statica, a configurazione salvata):
        `DetailRoutePrefix` verificato contro lo stesso registro già
        introdotto in fase 12 (`InternalUrlPath`) — non deve collidere con
        nessuno slug di `CmsPage`, nessun percorso `ExternalUrl` di menu, né
        con il `DetailRoutePrefix` di un'altra entità.
      - **Unicità dei valori a runtime** (dinamica, per singolo record): che i
        valori di `DetailKeyField` siano effettivamente univoci riga per riga
        è una responsabilità dei dati stessi (la tabella legacy sottostante),
        non qualcosa che il CMS può imporre senza alterare lo schema fisico —
        coerente con il principio già adottato per le tabelle FFM/legacy
        (integrità solo applicativa, mai vincoli fisici aggiunti dal CMS). Il
        CMS può al più segnalarlo/documentarlo, non farlo rispettare.
      - Il **routing runtime** (far corrispondere l'URL in ingresso al
        `DetailRoutePrefix` giusto ed estrarne il record) resta comunque
        responsabilità del progetto host, coerentemente con l'architettura
        "il CMS genera/valida i metadati, l'host renderizza".
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
