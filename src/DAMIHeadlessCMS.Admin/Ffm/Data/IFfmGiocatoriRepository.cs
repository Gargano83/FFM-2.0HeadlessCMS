using DAMIHeadlessCMS.Admin.Ffm.Models;

namespace DAMIHeadlessCMS.Admin.Ffm.Data;

/// <summary>
/// Accesso dati dedicato a FFM.Giocatori/FFM.SquadreRelGiocatori/FFM.Lega.
/// A differenza di IGenericEntityRepository (metadata-driven, valido per
/// qualunque tabella scaffoldata) questo repository è scritto a mano perché
/// la logica applicativa (stagione attiva, sincronizzazione massiva da
/// import Excel) è troppo specifica per essere dedotta da metadati generici.
/// SQL parametrico via ADO.NET, coerente con lo stile del resto del CMS.
/// </summary>
public interface IFfmGiocatoriRepository
{
    Task<IReadOnlyList<GiocatoreDto>> GetAllAsync(CancellationToken ct = default);

    Task<GiocatoreDto?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<GiocatoreDto> CreateAsync(GiocatoreDto giocatore, CancellationToken ct = default);

    Task UpdateAsync(int id, GiocatoreDto giocatore, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Sincronizzazione massiva (import Excel): allinea FFM.Giocatori all'elenco
    /// fornito. ATTENZIONE — comportamento ereditato 1:1 dal sistema legacy:
    /// i giocatori PRESENTI in FFM.Giocatori ma ASSENTI dall'elenco importato
    /// vengono ELIMINATI. Aggiorna inoltre ValoreDiMercato/Stipendio nelle righe
    /// attive di FFM.SquadreRelGiocatori per la stagione attualmente attiva
    /// (letta da FFM.Lega). Se non esiste alcuna stagione attiva, l'operazione
    /// non ha alcun effetto (nessuna eccezione: comportamento legacy preservato).
    /// </summary>
    Task ImportAsync(IReadOnlyList<GiocatoreDto> giocatori, CancellationToken ct = default);
}
