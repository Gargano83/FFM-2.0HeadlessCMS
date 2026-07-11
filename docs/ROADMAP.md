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

## Prossime fasi

- [ ] **10. Localizzazione multi-lingua nel backoffice**: attualmente il CMS
      legge/scrive sempre e solo nella lingua di default configurata (sia per
      `LocalizationSource` generiche sia per il modulo FFM, dove l'id lingua è
      un parametro fisso passato a `AddDAMIHeadlessCMSFfm`). Un selettore
      lingua nel backoffice (per editor multi-lingua sui campi localizzati)
      resta fuori dal perimetro attuale, deferita a quando servirà davvero.
- [ ] **12. Unicità URL nella creazione del menu**: validazione che impedisca
      la creazione di voci di menu (pagine/link esterni) con `Slug`/URL
      duplicati all'interno dell'albero — rilevante soprattutto in vista di
      relazioni categoria → documento (es. `WN_CATEGORIE` → una futura
      `WN_DOCUMENTI`, non ancora scaffoldata) dove URL duplicati causerebbero
      problemi di routing.
- [ ] **13. `appsettings` per ambiente + pulizia repository**: adozione del
      pattern `appsettings.{Environment}.json` (es.
      `appsettings.Development.json`/`appsettings.Production.json`) al posto
      di un unico `appsettings.json` con credenziali reali; verifica della
      cronologia Git per eventuali commit passati con segreti (vedi nota di
      sicurezza alla fase 6.3) ed eventuale rotazione delle credenziali
      coinvolte.
- [ ] **14. Dashboard post-login arricchita**: la pagina che segue il login
      (oggi il solo elenco "Entità gestite") arricchita con funzionalità utili
      al backoffice — da valutare insieme (es. tracciamento attività
      dell'utente loggato, contatori/riepiloghi sulle entità gestite, ultime
      modifiche).
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
