using DAMIHeadlessCMS.Admin.Ffm.Models;

namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>
/// Vista di una squadra nell'Area Riservata (propria o "altra squadra").
/// Riusa direttamente i DTO del modulo FFM (già costruiti in fase 7 per il
/// backoffice) invece di duplicarli — stessi dati, stessa fonte.
/// </summary>
public class SquadraViewModel
{
    public required InfoSquadraDto Info { get; init; }

    public required IReadOnlyList<GiocatoreSquadraDto> Rosa { get; init; }

    /// <summary>True solo per la propria squadra, e solo se Info.AbilitaModifica lo consente (es. mercato aperto).</summary>
    public bool PuoModificare { get; init; }
}
