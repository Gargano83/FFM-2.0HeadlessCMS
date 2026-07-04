using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DAMIHeadlessCMS.Data.Extensions;

/// <summary>
/// Punto di ingresso per integrare DAMIHeadlessCMS nell'app host. Esempio d'uso in
/// Program.cs dell'app MVC host:
///
///   builder.Services.AddDAMIHeadlessCMSData(builder.Configuration.GetConnectionString("Default")!);
///
/// Nelle fasi successive della roadmap questa classe si arricchirà con
/// AddDAMIHeadlessCMSAdmin() (Area Razor + routing) e AddDAMIHeadlessCMSIdentity() (Identity).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDAMIHeadlessCMSData(
        this IServiceCollection services,
        string connectionString,
        Action<SqlServerDbContextOptionsBuilder>? sqlServerOptions = null)
    {
        services.AddDbContext<CmsDbContext>(options =>
            options.UseSqlServer(connectionString, sqlServerOptions));

        return services;
    }
}
