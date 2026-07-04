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
/// La connection string viene letta da appsettings.json (da creare copiando
/// appsettings.example.json — vedi README) oppure dalla variabile d'ambiente
/// DAMIHEADLESSCMS_CONNECTIONSTRING. appsettings.json NON va committato in git.
/// </summary>
public class CmsDbContextFactory : IDesignTimeDbContextFactory<CmsDbContext>
{
    public CmsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("Default")
            ?? configuration["DAMIHEADLESSCMS_CONNECTIONSTRING"]
            ?? throw new InvalidOperationException(
                "Connection string 'Default' non trovata per la creazione a design-time di CmsDbContext. " +
                "Crea 'src/DAMIHeadlessCMS.Data/appsettings.json' copiando appsettings.example.json e valorizzando " +
                "ConnectionStrings:Default, oppure imposta la variabile d'ambiente DAMIHEADLESSCMS_CONNECTIONSTRING.");

        var optionsBuilder = new DbContextOptionsBuilder<CmsDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new CmsDbContext(optionsBuilder.Options);
    }
}
