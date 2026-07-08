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

## Prossime fasi

- [ ] **7. Pagine custom con componenti Angular/Syncfusion embeddati (solo
      `CmsAdmin`)**: due pagine di backoffice dedicate, riservate al ruolo
      `CmsAdmin`, che ospitano ciascuna un componente Angular basato su
      Syncfusion (Grid), reso indipendente dalla view legacy che lo ospitava
      originariamente:
  - [x] **7.1 Gestione `FFM.Giocatori`** — **implementata**:
        - Backend: modulo opzionale `DAMIHeadlessCMS.Admin.Ffm` (opt-in via
          `AddDAMIHeadlessCMSFfm(connectionString)`), con
          `FfmGiocatoriRepository` (ADO.NET dedicato, non metadata-driven —
          logica troppo specifica per il CRUD generico), `FfmGiocatoriApiController`
          (`/dami/ffm/api/giocatori`, CRUD + import Excel) e `FfmController`
          (`/dami/ffm/giocatori`, pagina di hosting), tutti riservati a
          `CmsAdmin`. Sostituisce integralmente il vecchio `SyncfusionController`
          legacy per la parte Giocatori.
        - Frontend: componente Angular aggiornato da Angular 15 ad Angular 17,
          Syncfusion EJ2 alla versione più recente compatibile (licenza
          community invariata), **rifattorizzato da app Angular "intera" a
          Custom Element** (`<dami-ffm-giocatori-grid>`, via `@angular/elements`)
          — nessuna dipendenza da `window.appSettings`, parametri passati come
          attributi HTML, build a bundle singolo con `ngx-build-plus` per
          l'inclusione diretta nella Razor view. Chiave di licenza Syncfusion
          spostata da hardcoded a configurazione server-side
          (`DAMIHeadlessCMS:Ffm:SyncfusionLicenseKey`).
        - ⚠️ **Comportamento preservato dal legacy, da tenere a mente**: l'import
          Excel sincronizza l'intero `FFM.Giocatori` con il file caricato,
          **eliminando i giocatori non presenti nel file**. Replicato
          identico per fedeltà funzionale, con conferma esplicita richiesta
          all'utente lato UI prima dell'operazione.
        - Da fare prima del rilascio: eseguire `npm install`/build reale del
          progetto Angular (non verificato in ambiente di sviluppo di Claude,
          privo di toolchain Node/Angular) e copiare il bundle compilato in
          `DAMIHeadlessCMS.Admin/wwwroot/ffm-widgets/giocatori/`.
  - [ ] **7.2 Gestione `FFM.SquadreRelGiocatori`**: stessa logica del punto
        7.1, applicata al componente Angular che gestisce la tabella di
        relazione `FFM.SquadreRelGiocatori` (associazione Giocatori↔Squadre).
        Il vecchio `AppComponent`/`ModalComponent` (ramo "Squadra") restano nel
        progetto Angular come riferimento per questo porting, non ancora
        migrati a Custom Element.
  - Nota architetturale: queste due pagine **non** passano dal CRUD generico
    metadata-driven (`GenericEntityController`), perché la UI richiesta è un
    componente Angular/Syncfusion specifico con logica ad hoc (es. grid con
    editing inline, drag&drop tra squadre, ecc.), non riconducibile agli
    `EditorType` standard.
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

## Decisioni architetturali chiave

| Aspetto | Scelta |
|---|---|
| Packaging | Razor Class Library (NuGet), montata via routing MVC nell'app host |
| DB target | SQL Server |
| Generazione CRUD | Metadata-driven a runtime (`cms.EntityDefinition`/`cms.FieldDefinition`), nessuna ricompilazione richiesta |
| Lettura schema DB | Query dirette su `sys.tables`/`sys.columns`/`sys.foreign_keys` (no API interne EF) |
| Autenticazione | ASP.NET Core Identity dedicato al backoffice, ruoli `CmsAdmin`/`CmsEditor` |
| Contenuti pagine custom | JSON a blocchi, ogni blocco può referenziare un componente esterno |
| Localizzazione legacy | Pattern "a chiave condivisa" descritto da `LocalizationSource`, nessuna FK fisica |
| Componenti Angular ad hoc (fase 7) | Pagine di backoffice dedicate, fuori dal CRUD generico, riservate a `CmsAdmin` |
| Contenuti statici esterni (fase 8) | Serviti dal progetto host, il CMS espone solo il link nel menu |
