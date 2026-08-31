using DAMIHeadlessCMS.Admin.Ffm.Models;

namespace DAMIHeadlessCMS.Admin.Ffm.Data;

/// <summary>
/// Accesso dati per la gestione della rosa di una squadra (FFM.Squadre,
/// FFM.SquadreRelGiocatori, FFM.Giocatori, FFM.Lega). Scritto a mano
/// (non metadata-driven) per le stesse ragioni di FfmGiocatoriRepository:
/// logica di dominio troppo specifica (stagione attiva, aggregati
/// finanziari, localizzazione del nome squadra) per il CRUD generico.
/// </summary>
public interface IFfmSquadraRepository
{
    /// <summary>Tutte le squadre — per l'amministrazione (backoffice): nessun filtro, stesso comportamento dell'endpoint legacy "api/club/squadre".</summary>
    Task<IReadOnlyList<SquadraListItemDto>> GetSquadreListAsync(CancellationToken ct = default);

    /// <summary>
    /// Solo le squadre "attive" — stesso filtro dell'endpoint legacy
    /// "api/club/squadreattive": una squadra è attiva se ha un utente
    /// WN_UTENTI con UT_TIPOLOGIA = 4 (presidente) e UT_attivo = 1 associato
    /// (UT_Squadra). Pensato per il selettore dell'Area Riservata — non usare
    /// per l'elenco squadre del backoffice, che deve restare completo.
    /// </summary>
    Task<IReadOnlyList<SquadraListItemDto>> GetSquadreAttiveAsync(CancellationToken ct = default);

    Task<InfoSquadraDto?> GetInfoSquadraAsync(int idSquadra, CancellationToken ct = default);

    /// <summary>
    /// Aggiorna i soli campi anagrafici di FFM.Squadre esposti in modifica
    /// (Presidente, VicePresidente, Allenatore, NomeStadio) — non i campi
    /// finanziari né di contratto allenatore. Nessun controllo di autorizzazione
    /// qui: va ri-applicato dal chiamante (stessa convenzione di
    /// <see cref="AggiornaDettaglioGiocatorePerSquadraAsync"/>).
    /// </summary>
    Task AggiornaInfoSquadraAsync(int idSquadra, string? presidente, string? vicePresidente, string? allenatore, string? nomeStadio, CancellationToken ct = default);

    /// <summary>Rosa completa di una squadra per la stagione attiva, ordinata come nel sistema legacy (ruolo, stipendio, valore, nome).</summary>
    Task<IReadOnlyList<GiocatoreSquadraDto>> GetRosaAsync(int idSquadra, CancellationToken ct = default);

    Task<GiocatoreSquadraDto?> GetDettaglioGiocatorePerSquadraAsync(int idSquadra, int idGiocatore, CancellationToken ct = default);

    /// <summary>Giocatori non presenti in nessuna FFM.SquadreRelGiocatori (per il selettore "aggiungi giocatore").</summary>
    Task<IReadOnlyList<GiocatoreSvincolatoDto>> GetGiocatoriSvincolatiAsync(CancellationToken ct = default);

    /// <summary>
    /// Come <see cref="GetGiocatoriSvincolatiAsync"/> ma filtrato per nome/cognome
    /// (LIKE, parametrizzato) e limitato a <paramref name="limit"/> righe — pensato
    /// per un campo di ricerca con autocompletamento (Area Riservata) dove l'elenco
    /// completo di tutti gli svincolati sarebbe troppo lungo da mostrare.
    /// </summary>
    Task<IReadOnlyList<GiocatoreSvincolatoDto>> CercaGiocatoriSvincolatiAsync(string query, int limit = 15, CancellationToken ct = default);

    /// <summary>
    /// Aggiorna solo la colonna FFM.Squadre.LogoStatistiche — scrittura mirata,
    /// pensata per la migrazione one-off dei loghi squadra dal sito legacy verso
    /// lo storage locale (vedi TestHost/PublicSite/LegacyMediaMigrationService),
    /// non per l'editing generico di FFM.Squadre (che passa dal CRUD scaffolded
    /// standard, /dami/{entityId}).
    /// </summary>
    Task UpdateLogoStatisticheAsync(int idSquadra, string relativePath, CancellationToken ct = default);

    /// <summary>Aggiunge un giocatore svincolato alla rosa, per la stagione attualmente attiva. Nessun effetto se non c'è una stagione attiva.</summary>
    Task AggiungiGiocatorePerSquadraAsync(int idSquadra, int idGiocatore, decimal? valoreDiMercato, decimal? stipendio, int? idUtente, CancellationToken ct = default);

    Task EliminaGiocatorePerSquadraAsync(int idSquadra, int idGiocatore, CancellationToken ct = default);

    /// <summary>Aggiorna stato e ruoli specifici (vedi <see cref="RuoloRosaCodes"/>) di un giocatore in rosa.</summary>
    Task AggiornaDettaglioGiocatorePerSquadraAsync(int idSquadra, int idGiocatore, string? stato, IReadOnlyList<string>? ruoliSpecifici, int? idUtente, CancellationToken ct = default);
}
