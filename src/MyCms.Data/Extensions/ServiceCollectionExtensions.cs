using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MyCms.Data.Extensions;

/// <summary>
/// Punto di ingresso per integrare MyCms nell'app host. Esempio d'uso in
/// Program.cs dell'app MVC host:
///
///   builder.Services.AddMyCmsData(builder.Configuration.GetConnectionString("Default")!);
///
/// Nelle fasi successive della roadmap questa classe si arricchirà con
/// AddMyCmsAdmin() (Area Razor + routing) e AddMyCmsIdentity() (Identity).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMyCmsData(
        this IServiceCollection services,
        string connectionString,
        Action<SqlServerDbContextOptionsBuilder>? sqlServerOptions = null)
    {
        services.AddDbContext<CmsDbContext>(options =>
            options.UseSqlServer(connectionString, sqlServerOptions));

        return services;
    }
}
