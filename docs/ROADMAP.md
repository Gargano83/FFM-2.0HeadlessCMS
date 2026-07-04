# Roadmap di sviluppo — DAMIHeadlessCMS

CMS .NET 10, distribuito come libreria (Razor Class Library), integrabile in
un'applicazione MVC host. Approccio scelto per CRUD/scaffolding: **metadata-driven
runtime rendering** (nessuna generazione di file .cs/.cshtml, tutto pilotato da
metadati salvati nello schema `cms.*` del database).

## Fasi

- [x] **1. Core**: schema tabelle di sistema (`cms.EntityDefinition`,
      `cms.FieldDefinition`, `cms.Page`, `cms.Menu`, `cms.MenuItem`) + `DbContext`
      del CMS + prima migration. *(migration applicata con successo)*
- [x] **2. Scaffolding engine**: lettura schema DB SQL Server tramite query
      dirette su `sys.tables`/`sys.columns`/`sys.foreign_keys` (niente API
      interne EF) + `ScaffoldingService` che popola `EntityDefinition`/
      `FieldDefinition` con inferenza di `EditorType`, idempotente su
      riesecuzioni. Verificabile tramite il wizard di scaffolding nel
      backoffice (fase 3).
- [ ] **3. Generic CRUD**: `IGenericEntityRepository` (accesso dati generico via
      SQL parametrico/Dapper, whitelisting colonne dai metadati) + controller
      generico (`/cms-admin/entities/{entityName}`) + editor template Razor
      (Text, TextArea, Number, Checkbox, Date, Select, File).
- [ ] **4. Identity**: integrazione ASP.NET Core Identity di default, ruoli
      `CmsAdmin`/`CmsEditor`, protezione dell'Area `CmsAdmin`.
- [ ] **5. Page builder + Menu**: modello a blocchi (`ContentJson`) per pagine
      custom con componenti embeddabili (Angular, ecc.), editor drag&drop per
      la struttura del menu.
- [ ] **6. Editor avanzati**: upload file, RichText editor, select FK con
      ricerca/autocomplete.

## Decisioni architetturali chiave

| Aspetto | Scelta |
|---|---|
| Packaging | Razor Class Library (NuGet), montata via Area nell'app host |
| DB target (fase 1) | SQL Server |
| Generazione CRUD | Metadata-driven a runtime, nessuna ricompilazione richiesta |
| Lettura schema DB | `IDatabaseModelFactory` (stesso motore di `Scaffold-DbContext`) |
| Autenticazione | ASP.NET Core Identity default, nessuna libreria custom |
| Contenuti pagine custom | JSON a blocchi, ogni blocco può referenziare un componente esterno |
