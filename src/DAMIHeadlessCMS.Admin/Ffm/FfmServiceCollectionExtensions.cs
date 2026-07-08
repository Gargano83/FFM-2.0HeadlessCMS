using Microsoft.Extensions.DependencyInjection;
using DAMIHeadlessCMS.Admin.Ffm.Data;

namespace DAMIHeadlessCMS.Admin.Ffm;

/// <summary>
/// Modulo OPZIONALE del backoffice per la gestione di tabelle FFM specifiche
/// (FFM.Giocatori, e in una fase successiva FFM.SquadreRelGiocatori) tramite
/// componenti Angular/Syncfusion dedicati, fuori dal CRUD generico
/// metadata-driven. Da chiamare solo se l'host ospita effettivamente quello
/// schema — non è parte del core del CMS:
///
///   builder.Services.AddDAMIHeadlessCMSData(connectionString);
///   builder.Services.AddDAMIHeadlessCMSAdmin(connectionString);
///   builder.Services.AddDAMIHeadlessCMSFfm(connectionString);
/// </summary>
public static class FfmServiceCollectionExtensions
{
    public static IServiceCollection AddDAMIHeadlessCMSFfm(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<IFfmGiocatoriRepository>(_ => new FfmGiocatoriRepository(connectionString));

        return services;
    }
}
