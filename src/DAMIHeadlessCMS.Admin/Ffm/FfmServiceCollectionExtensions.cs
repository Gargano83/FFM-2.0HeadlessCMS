using Microsoft.Extensions.DependencyInjection;
using DAMIHeadlessCMS.Admin.Ffm.Data;

namespace DAMIHeadlessCMS.Admin.Ffm;

/// <summary>
/// Modulo OPZIONALE del backoffice per la gestione di tabelle FFM specifiche
/// (FFM.Giocatori, FFM.SquadreRelGiocatori) tramite componenti
/// Angular/Syncfusion dedicati, fuori dal CRUD generico metadata-driven. Da
/// chiamare solo se l'host ospita effettivamente quello schema — non è parte
/// del core del CMS:
///
///   builder.Services.AddDAMIHeadlessCMSData(connectionString);
///   builder.Services.AddDAMIHeadlessCMSAdmin(connectionString);
///   builder.Services.AddDAMIHeadlessCMSFfm(connectionString);
/// </summary>
public static class FfmServiceCollectionExtensions
{
    /// <param name="defaultLanguageId">
    /// Id lingua (tabella lingue legacy, es. WN_LINGUE) usato per risolvere
    /// FFM.Squadre.Nome tramite dbo.udf_Localize finché il backoffice non ha
    /// un selettore multi-lingua. Default 1 (Italiano nella maggior parte
    /// delle configurazioni legacy viste finora nel progetto) — verifica e
    /// correggi se nel tuo database l'id è diverso.
    /// </param>
    public static IServiceCollection AddDAMIHeadlessCMSFfm(
        this IServiceCollection services, string connectionString, int defaultLanguageId = 1)
    {
        services.AddScoped<IFfmGiocatoriRepository>(_ => new FfmGiocatoriRepository(connectionString));
        services.AddScoped<IFfmSquadraRepository>(_ => new FfmSquadraRepository(connectionString, defaultLanguageId));
        services.AddScoped<IFfmUserResolver>(_ => new FfmUserResolver(connectionString));

        // Personalizzazione divisa squadra (FFM.DivisaTemplate + FFM.SquadreMaglia,
        // vedi piano "Personalizzazione divisa squadra"): modello dati, motore di
        // rendering Canvas 2D ed endpoint GET/PUT sviluppati e validati dentro
        // DAMIHeadlessCMS.TestHost (fasi 1-5) — integrazione sull'host reale
        // FFM2.0Core ancora da fare (fase 7), a valle anche della UI (fase 6).
        services.AddScoped<IFfmDivisaTemplateRepository>(_ => new FfmDivisaTemplateRepository(connectionString));
        services.AddScoped<IFfmDivisaRepository>(_ => new FfmDivisaRepository(connectionString));

        return services;
    }
}
