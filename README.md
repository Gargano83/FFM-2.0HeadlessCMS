# MyCms — Fase 1: Core + Data

Scheletro dei progetti `MyCms.Core` (entità di dominio) e `MyCms.Data`
(`CmsDbContext` + configurazioni Fluent API) per le tabelle di sistema del
CMS, come da `docs/ROADMAP.md`.

## Cosa contiene

- **MyCms.Core**: entità `EntityDefinition`, `FieldDefinition`, `CmsPage`,
  `CmsMenu`, `CmsMenuItem` + enum `EditorType`, `MenuTargetType`. Nessuna
  dipendenza da EF Core: sono POCO puri, riutilizzabili anche da un eventuale
  livello di API/servizi senza portarsi dietro EF.
- **MyCms.Data**: `CmsDbContext` (schema `cms`), configurazioni Fluent API,
  extension method `AddMyCmsData(...)` per la registrazione DI nell'app host.

## Setup

1. Verifica di avere l'SDK .NET 10 installato:
   ```
   dotnet --version
   ```
2. Ripristina i pacchetti:
   ```
   dotnet restore
   ```
   > Nota: nel `.csproj` di `MyCms.Data` ho fissato le versioni dei pacchetti
   > `Microsoft.EntityFrameworkCore.SqlServer` / `.Design` a `10.0.0` come
   > placeholder coerente con il target .NET 10: verifica lato tuo l'ultima
   > versione stabile pubblicata su NuGet.org e allineala se necessario.

3. **Crea la connection string per il design-time.** `MyCms.Data` è una
   libreria: non ha un `Program.cs` che registra la DI, quindi gli strumenti
   `dotnet ef` non saprebbero come istanziare `CmsDbContext`. Per questo il
   progetto include `CmsDbContextFactory` (`IDesignTimeDbContextFactory<CmsDbContext>`),
   che gli strumenti EF trovano ed eseguono automaticamente. Questa factory
   legge la connection string da `src/MyCms.Data/appsettings.json` (file
   locale, NON committato in git — vedi `.gitignore`):
   ```
   cp src/MyCms.Data/appsettings.example.json src/MyCms.Data/appsettings.json
   ```
   e poi valorizza `ConnectionStrings:Default` con i tuoi dati reali.

   In alternativa, senza toccare file, puoi impostare la variabile
   d'ambiente `MYCMS_CONNECTIONSTRING`.

   > Attenzione alla sintassi della connection string: `Trusted_Connection=True`
   > (Windows Authentication) e `User Id`/`Password` (SQL Authentication) sono
   > **due modalità alternative**, non vanno mai combinate insieme. Per
   > un'istanza SQL Server con utente `sa` (es. container Docker) usa:
   > `Server=host,1433;Database=NomeDb;User Id=sa;Password=...;TrustServerCertificate=True;`

4. Genera la prima migration (schema `cms.*`), dalla cartella `src/MyCms.Data`:
   ```
   dotnet tool install --global dotnet-ef   # se non già installato
   dotnet ef migrations add InitialCmsSchema
   ```
   Non serve più `--startup-project`: la design-time factory rende
   `MyCms.Data` autosufficiente per gli strumenti EF.

5. Applica la migration al DB:
   ```
   dotnet ef database update
   ```

## Uso nell'app host (anteprima)

```csharp
// Program.cs dell'app MVC host
builder.Services.AddMyCmsData(builder.Configuration.GetConnectionString("Default")!);
```

## Fase 2: scaffolding engine

`MyCms.Scaffolding` legge lo schema di SQL Server tramite query dirette su
`sys.tables`/`sys.columns`/`sys.foreign_keys` (nessuna dipendenza da API
interne di EF Core) e popola `cms.EntityDefinition`/`cms.FieldDefinition`
con l'`EditorType` dedotto automaticamente dal tipo SQL di ogni colonna.

### Come testarlo

Non abbiamo ancora la UI del wizard (arriverà nella fase 3), quindi il modo
più rapido per verificare lo scaffolding contro un DB reale è la console
`MyCms.Scaffolding.ConsoleTest`:

```
cd src/MyCms.Scaffolding.ConsoleTest
cp appsettings.example.json appsettings.json
# valorizza ConnectionStrings:Default con i tuoi dati reali
dotnet run
```

La console elenca le tabelle disponibili (tutte tranne lo schema `cms` e
`__EFMigrationsHistory`), ti chiede quali scaffoldare, e stampa il risultato:
per ogni entità, le colonne con `EditorType` dedotto e l'eventuale FK
risolta.

Rilanciandola più volte sulle stesse tabelle l'operazione è **idempotente**:
aggiorna i metadati strutturali (tipi, nuove colonne, FK) ma preserva le
personalizzazioni già fatte manualmente in `cms.FieldDefinition`
(`DisplayName`, `EditorType`, `ShowInList`/`ShowInForm`, `SortOrder`).

## Prossimo step (fase 3 della roadmap)

Generic CRUD: `IGenericEntityRepository` (accesso dati generico via SQL
parametrico guidato dai metadati) + controller generico + editor template
Razor, impacchettati nella Razor Class Library `MyCms.Admin` insieme alla UI
del wizard di scaffolding.
