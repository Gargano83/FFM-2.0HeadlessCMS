using Microsoft.Extensions.DependencyInjection;

namespace DAMIHeadlessCMS.Scaffolding.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra ISqlServerSchemaReader e ScaffoldingService. Da chiamare insieme
    /// ad AddDAMIHeadlessCMSData(...) nel Program.cs dell'app host:
    ///
    ///   builder.Services.AddDAMIHeadlessCMSData(connectionString);
    ///   builder.Services.AddDAMIHeadlessCMSScaffolding(connectionString);
    /// </summary>
    public static IServiceCollection AddDAMIHeadlessCMSScaffolding(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<ISqlServerSchemaReader>(_ => new SqlServerSchemaReader(connectionString));
        services.AddScoped<ScaffoldingService>();

        return services;
    }
}
