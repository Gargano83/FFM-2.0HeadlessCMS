using Microsoft.Extensions.DependencyInjection;

namespace MyCms.Scaffolding.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra ISqlServerSchemaReader e ScaffoldingService. Da chiamare insieme
    /// ad AddMyCmsData(...) nel Program.cs dell'app host:
    ///
    ///   builder.Services.AddMyCmsData(connectionString);
    ///   builder.Services.AddMyCmsScaffolding(connectionString);
    /// </summary>
    public static IServiceCollection AddMyCmsScaffolding(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<ISqlServerSchemaReader>(_ => new SqlServerSchemaReader(connectionString));
        services.AddScoped<ScaffoldingService>();

        return services;
    }
}
