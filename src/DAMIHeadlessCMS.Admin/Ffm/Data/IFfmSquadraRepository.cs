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
    /// <summary>Elenco leggero delle squadre (pagina indice /dami/ffm/squadre), con nome già localizzato.</summary>
    Task<IReadOnlyList<SquadraListItemDto>> GetSquadreListAsync(CancellationToken ct = default);

    Task<InfoSquadraDto?> GetInfoSquadraAsync(int idSquadra, CancellationToken ct = default);

    /// <summary>Rosa completa di una squadra per la stagione attiva, ordinata come nel sistema legacy (ruolo, stipendio, valore, nome).</summary>
    Task<IReadOnlyList<GiocatoreSquadraDto>> GetRosaAsync(int idSquadra, CancellationToken ct = default);

    Task<GiocatoreSquadraDto?> GetDettaglioGiocatorePerSquadraAsync(int idSquadra, int idGiocatore, CancellationToken ct = default);

    /// <summary>Giocatori non presenti in nessuna FFM.SquadreRelGiocatori (per il selettore "aggiungi giocatore").</summary>
    Task<IReadOnlyList<GiocatoreSvincolatoDto>> GetGiocatoriSvincolatiAsync(CancellationToken ct = default);

    /// <summary>Aggiunge un giocatore svincolato alla rosa, per la stagione attualmente attiva. Nessun effetto se non c'è una stagione attiva.</summary>
    Task AggiungiGiocatorePerSquadraAsync(int idSquadra, int idGiocatore, decimal? valoreDiMercato, decimal? stipendio, int? idUtente, CancellationToken ct = default);

    Task EliminaGiocatorePerSquadraAsync(int idSquadra, int idGiocatore, CancellationToken ct = default);

    Task AggiornaDettaglioGiocatorePerSquadraAsync(int idSquadra, int idGiocatore, int mesi, string? stato, int? idUtente, CancellationToken ct = default);
}
