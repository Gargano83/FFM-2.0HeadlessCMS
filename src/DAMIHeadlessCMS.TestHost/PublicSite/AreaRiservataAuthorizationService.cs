using Microsoft.Extensions.Configuration;

namespace DAMIHeadlessCMS.TestHost.PublicSite;

/// <summary>
/// Regole di autorizzazione per le scritture sulla rosa nell'Area Riservata,
/// condivise tra <see cref="Controllers.AreaRiservataController"/> (pagine,
/// caricamento iniziale) e <see cref="Controllers.AreaRiservataApiController"/>
/// (azioni via fetch, senza reload di pagina) — stessa regola applicata in
/// entrambi i punti, mai duplicata: una squadra è modificabile dall'utente
/// corrente se è la propria (e il mercato è aperto, InfoSquadraDto.AbilitaModifica)
/// oppure se l'utente è uno dei "super-admin" configurati in
/// <c>PublicSite:AreaRiservataSuperAdminUserIds</c> — nel qual caso può
/// modificare qualunque squadra, sempre soggetto allo stesso AbilitaModifica
/// della squadra bersaglio (un super-admin non forza l'edit a mercato chiuso,
/// si limita a poter editare anche squadre non proprie).
/// </summary>
public class AreaRiservataAuthorizationService
{
    private readonly IConfiguration _configuration;

    public AreaRiservataAuthorizationService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsSuperAdmin(int? idUtente) =>
        idUtente is int id && SuperAdminUserIds.Contains(id);

    /// <summary>
    /// True se l'utente (idUtente/idSquadraUtente, entrambi dai claim del cookie
    /// PublicUser) può scrivere sulla squadra idSquadraTarget, dato il valore
    /// corrente di AbilitaModifica per quella squadra. Va sempre ri-verificato
    /// lato server per ogni singola azione di scrittura — mai fidarsi di un
    /// PuoModificare calcolato lato client o mostrato in una risposta precedente.
    /// </summary>
    public bool CanEdit(int idSquadraTarget, int? idSquadraUtente, int? idUtente, bool abilitaModifica) =>
        abilitaModifica && (idSquadraTarget == idSquadraUtente || IsSuperAdmin(idUtente));

    private IReadOnlySet<int> SuperAdminUserIds =>
        (_configuration.GetSection("PublicSite:AreaRiservataSuperAdminUserIds").Get<int[]>() ?? []).ToHashSet();
}
