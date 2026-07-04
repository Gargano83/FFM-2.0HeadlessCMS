# DAMIHeadlessCMS — Fase 1: Core + Data

Scheletro dei progetti `DAMIHeadlessCMS.Core` (entità di dominio) e `DAMIHeadlessCMS.Data`
(`CmsDbContext` + configurazioni Fluent API) per le tabelle di sistema del
CMS, come da `docs/ROADMAP.md`.

## Cosa contiene

- **DAMIHeadlessCMS.Core**: entità `EntityDefinition`, `FieldDefinition`, `CmsPage`,
  `CmsMenu`, `CmsMenuItem` + enum `EditorType`, `MenuTargetType`. Nessuna
  dipendenza da EF Core: sono POCO puri, riutilizzabili anche da un eventuale
  livello di API/servizi senza portarsi dietro EF.
- **DAMIHeadlessCMS.Data**: `CmsDbContext` (schema `cms`), configurazioni Fluent API,
  extension method `AddDAMIHeadlessCMSData(...)` per la registrazione DI nell'app host.

## Setup

1. Verifica di avere l'SDK .NET 10 installato:
   ```
   dotnet --version
   ```
2. Ripristina i pacchetti:
   ```
   dotnet restore
   ```
   > Nota: nel `.csproj` di `DAMIHeadlessCMS.Data` ho fissato le versioni dei pacchetti
   > `Microsoft.EntityFrameworkCore.SqlServer` / `.Design` a `10.0.0` come
   > placeholder coerente con il target .NET 10: verifica lato tuo l'ultima
   > versione stabile pubblicata su NuGet.org e allineala se necessario.

3. **Crea la connection string per il design-time.** `DAMIHeadlessCMS.Data` è una
   libreria: non ha un `Program.cs` che registra la DI, quindi gli strumenti
   `dotnet ef` non saprebbero come istanziare `CmsDbContext`. Per questo il
   progetto include `CmsDbContextFactory` (`IDesignTimeDbContextFactory<CmsDbContext>`),
   che gli strumenti EF trovano ed eseguono automaticamente. Questa factory
   legge la connection string da `src/DAMIHeadlessCMS.Data/appsettings.json` (file
   locale, NON committato in git — vedi `.gitignore`):
   ```
   cp src/DAMIHeadlessCMS.Data/appsettings.example.json src/DAMIHeadlessCMS.Data/appsettings.json
   ```
   e poi valorizza `ConnectionStrings:Default` con i tuoi dati reali.

   In alternativa, senza toccare file, puoi impostare la variabile
   d'ambiente `DAMIHEADLESSCMS_CONNECTIONSTRING`.

   > Attenzione alla sintassi della connection string: `Trusted_Connection=True`
   > (Windows Authentication) e `User Id`/`Password` (SQL Authentication) sono
   > **due modalità alternative**, non vanno mai combinate insieme. Per
   > un'istanza SQL Server con utente `sa` (es. container Docker) usa:
   > `Server=host,1433;Database=NomeDb;User Id=sa;Password=...;TrustServerCertificate=True;`

4. Genera la prima migration (schema `cms.*`), dalla cartella `src/DAMIHeadlessCMS.Data`:
   ```
   dotnet tool install --global dotnet-ef   # se non già installato
   dotnet ef migrations add InitialCmsSchema
   ```
   Non serve più `--startup-project`: la design-time factory rende
   `DAMIHeadlessCMS.Data` autosufficiente per gli strumenti EF.

5. Applica la migration al DB:
   ```
   dotnet ef database update
   ```

## Uso nell'app host (anteprima)

```csharp
// Program.cs dell'app MVC host
builder.Services.AddDAMIHeadlessCMSData(builder.Configuration.GetConnectionString("Default")!);
```

## Fase 2: scaffolding engine

`DAMIHeadlessCMS.Scaffolding` legge lo schema di SQL Server tramite query dirette su
`sys.tables`/`sys.columns`/`sys.foreign_keys` (nessuna dipendenza da API
interne di EF Core) e popola `cms.EntityDefinition`/`cms.FieldDefinition`
con l'`EditorType` dedotto automaticamente dal tipo SQL di ogni colonna.

### Come testarlo

Lo scaffolding si esegue dal **wizard nel backoffice** (`/backoffice/admin/scaffolding`,
riservato al ruolo `CmsAdmin`): un progetto console dedicato non è più necessario
ed è stato rimosso. Avvia `DAMIHeadlessCMS.TestHost`, accedi al backoffice e usa
il wizard per selezionare le tabelle da scaffoldare.

Rilanciando lo scaffold più volte sulle stesse tabelle (tasto "Aggiorna struttura"
nella vista Struttura, oppure riselezionandole nel wizard) l'operazione è
**idempotente**: aggiorna i metadati strutturali (tipi, nuove colonne, FK) ma
preserva le personalizzazioni già fatte manualmente in `cms.FieldDefinition`
(`DisplayName`, `EditorType`, `ShowInList`/`ShowInForm`, `SortOrder`).

## Stato del progetto

Questo README copre ancora principalmente le Fasi 1-2 della roadmap. Le Fasi
3-6 (Generic CRUD, Identity, Page Builder + Menu, Editor avanzati) sono già
implementate — vedi `docs/ROADMAP.md` per lo stato aggiornato di ciascuna fase.
