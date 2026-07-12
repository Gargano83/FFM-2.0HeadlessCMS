using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DAMIHeadlessCMS.Data;

/// <summary>
/// Usata SOLO dagli strumenti EF Core a design-time (dotnet ef migrations add,
/// dotnet ef database update) per poter istanziare CmsDbContext senza passare
/// da un host applicativo con Dependency Injection, dato che DAMIHeadlessCMS.Data è una
/// libreria e non ha un proprio Program.cs.
///
/// A runtime, l'app host NON usa questa classe: le DbContextOptions arrivano
/// invece da AddDAMIHeadlessCMSData(connectionString) registrato in Program.cs dell'host.
///
/// Stesso pattern a due file usato dall'host (vedi DAMIHeadlessCMS.TestHost):
///   - appsettings.json              → versionato, contiene solo un placeholder
///     ("CAMBIAMI") per la connection string, mai una credenziale reale.
///   - appsettings.Development.json  → locale, MAI committato (vedi .gitignore),
///     qui va la connection string reale. Va creato a mano copiando la
///     struttura di appsettings.json e valorizzandola.
/// La variabile d'ambiente DOTNET_ENVIRONMENT (o, in mancanza, ASPNETCORE_ENVIRONMENT)
/// seleziona il file di override; se nessuna delle due è impostata si assume
/// "Development", coerente con l'uso da riga di comando di dotnet ef.
/// </summary>
public class CmsDbContextFactory : IDesignTimeDbContextFactory<CmsDbContext>
{
    public CmsDbContext CreateDbContext(string[] args)
    {
        var environmentName =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("Default")
            ?? configuration["DAMIHEADLESSCMS_CONNECTIONSTRING"]
            ?? throw new InvalidOperationException(
                "Connection string 'Default' non trovata per la creazione a design-time di CmsDbContext. " +
                "Crea 'src/DAMIHeadlessCMS.Data/appsettings.Development.json' (locale, mai committato) con " +
                "ConnectionStrings:Default valorizzata, oppure imposta la variabile d'ambiente DAMIHEADLESSCMS_CONNECTIONSTRING.");

        var optionsBuilder = new DbContextOptionsBuilder<CmsDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new CmsDbContext(optionsBuilder.Options);
    }
}
