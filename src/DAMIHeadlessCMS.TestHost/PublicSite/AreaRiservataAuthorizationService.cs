using Microsoft.Extensions.Configuration;

namespace DAMIHeadlessCMS.TestHost.PublicSite;

/// <summary>
/// Regole di autorizzazione per le scritture sulla rosa nell'Area Riservata,
/// condivise tra <see cref="Controllers.AreaRiservataController"/> (pagine,
/// caricamento iniziale) e <see cref="Controllers.AreaRiservataApiController"/>
/// (azioni via fetch, senza reload di pagina) — stessa regola applicata in
/// entrambi i punti, mai duplicata. Due permessi distinti, non uno solo:
/// - <see cref="CanEdit"/>: modificare lo Stato di un giocatore già in rosa.
///   Proprietario della squadra (con AbilitaModifica) oppure super-admin.
/// - <see cref="CanAddOrRemove"/>: aggiungere un giocatore svincolato o
///   rimuoverne uno dalla rosa. Riservato ai soli super-admin (configurati in
///   <c>PublicSite:AreaRiservataSuperAdminUserIds</c>) — un proprietario può
///   modificare i propri giocatori ma non alterare la composizione della
///   rosa: quell'operazione segue un processo separato, fuori da questa
///   pagina. Entrambi i permessi restano comunque soggetti allo stesso
///   AbilitaModifica (mercato aperto) della squadra bersaglio.
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
    /// PublicUser) può modificare lo Stato di un giocatore della squadra
    /// idSquadraTarget, dato il valore corrente di AbilitaModifica per quella
    /// squadra. Va sempre ri-verificato lato server per ogni singola azione di
    /// scrittura — mai fidarsi di un PuoModificare calcolato lato client o
    /// mostrato in una risposta precedente.
    /// </summary>
    public bool CanEdit(int idSquadraTarget, int? idSquadraUtente, int? idUtente, bool abilitaModifica) =>
        abilitaModifica && (idSquadraTarget == idSquadraUtente || IsSuperAdmin(idUtente));

    /// <summary>
    /// True se l'utente corrente può aggiungere/rimuovere giocatori dalla rosa
    /// della squadra idSquadraTarget — riservato ai soli super-admin, anche
    /// sulla propria squadra: a differenza di <see cref="CanEdit"/>, non basta
    /// esserne il proprietario. Va sempre ri-verificato lato server, mai
    /// dedotto solo dalla UI (pulsanti nascosti non sono un controllo di
    /// sicurezza).
    /// </summary>
    public bool CanAddOrRemove(int? idUtente, bool abilitaModifica) =>
        abilitaModifica && IsSuperAdmin(idUtente);

    private IReadOnlySet<int> SuperAdminUserIds =>
        (_configuration.GetSection("PublicSite:AreaRiservataSuperAdminUserIds").Get<int[]>() ?? []).ToHashSet();
}
