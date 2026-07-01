using Microsoft.Extensions.DependencyInjection;
using MyCms.Admin.Data;

namespace MyCms.Admin.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra IGenericEntityRepository. Da chiamare insieme a
    /// AddMyCmsData(...) nel Program.cs dell'app host, con la STESSA
    /// connection string (le tabelle applicative vivono nello stesso DB
    /// dello schema cms.*):
    ///
    ///   builder.Services.AddMyCmsData(connectionString);
    ///   builder.Services.AddMyCmsAdmin(connectionString);
    /// </summary>
    public static IServiceCollection AddMyCmsAdmin(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<IGenericEntityRepository>(_ => new GenericEntityRepository(connectionString));

        return services;
    }
}
