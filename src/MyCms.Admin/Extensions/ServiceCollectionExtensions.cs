using Microsoft.Extensions.DependencyInjection;
using MyCms.Admin.Data;

namespace MyCms.Admin.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra IGenericEntityRepository e IFileStorageProvider (default: filesystem
    /// locale su wwwroot/uploads dell'host). Da chiamare insieme ad AddMyCmsData(...):
    ///
    ///   builder.Services.AddMyCmsData(connectionString);
    ///   builder.Services.AddMyCmsAdmin(connectionString);
    ///
    /// Per un provider di storage diverso (es. Azure Blob), registra IFileStorageProvider
    /// DOPO questa chiamata: l'ultima registrazione vince nel container DI.
    /// </summary>
    public static IServiceCollection AddMyCmsAdmin(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<IFileStorageProvider, LocalFileStorageProvider>();
        services.AddScoped<IGenericEntityRepository>(sp =>
            new GenericEntityRepository(connectionString, sp.GetRequiredService<IFileStorageProvider>()));

        return services;
    }
}