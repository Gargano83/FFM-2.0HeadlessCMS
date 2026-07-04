using Microsoft.Extensions.DependencyInjection;
using DAMIHeadlessCMS.Admin.Data;

namespace DAMIHeadlessCMS.Admin.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra IGenericEntityRepository e IFileStorageProvider (default: filesystem
    /// locale su wwwroot/uploads dell'host). Da chiamare insieme ad AddDAMIHeadlessCMSData(...):
    ///
    ///   builder.Services.AddDAMIHeadlessCMSData(connectionString);
    ///   builder.Services.AddDAMIHeadlessCMSAdmin(connectionString);
    ///
    /// Per un provider di storage diverso (es. Azure Blob), registra IFileStorageProvider
    /// DOPO questa chiamata: l'ultima registrazione vince nel container DI.
    /// </summary>
    public static IServiceCollection AddDAMIHeadlessCMSAdmin(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<IFileStorageProvider, LocalFileStorageProvider>();
        services.AddScoped<IGenericEntityRepository>(sp =>
            new GenericEntityRepository(connectionString, sp.GetRequiredService<IFileStorageProvider>()));

        return services;
    }
}