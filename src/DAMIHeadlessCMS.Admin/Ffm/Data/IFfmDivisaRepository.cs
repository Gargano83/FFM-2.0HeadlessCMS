using DAMIHeadlessCMS.Admin.Ffm.Models;

namespace DAMIHeadlessCMS.Admin.Ffm.Data;

/// <summary>
/// Accesso dati per la personalizzazione divisa (solo maglia) di una squadra
/// (<c>FFM.SquadreMaglia</c>). Scritto a mano (non metadata-driven), stessa
/// ragione di <see cref="IFfmSquadraRepository"/>: relazione 1:1 con
/// <c>FFM.Squadre</c> con logica di default propria, non un CRUD generico.
/// </summary>
public interface IFfmDivisaRepository
{
    /// <summary>
    /// Personalizzazione corrente della squadra. Se non esiste ancora una riga
    /// in <c>FFM.SquadreMaglia</c> per <paramref name="idSquadra"/>, restituisce
    /// un default sensato (primo template attivo per Ordine, colori neutri,
    /// <see cref="DivisaSquadraDto.NonAncoraPersonalizzata"/> = true) invece di
    /// null — la UI ha sempre qualcosa da mostrare. Restituisce null solo nel
    /// caso limite in cui il catalogo <c>FFM.DivisaTemplate</c> non abbia
    /// nessun template attivo da usare come default.
    /// </summary>
    Task<DivisaSquadraDto?> GetDivisaAsync(int idSquadra, CancellationToken ct = default);

    /// <summary>Crea (se non esiste ancora) o aggiorna la personalizzazione della squadra (upsert su <c>FFM.SquadreMaglia</c>).</summary>
    Task AggiornaDivisaAsync(int idSquadra, AggiornaDivisaRequestDto dto, int? idUtente, CancellationToken ct = default);
}
