using DAMIHeadlessCMS.TestHost.PublicSite;

namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>
/// Riga risultati Popa Libertadores: unica competizione a doppio turno (andata/ritorno,
/// due sedi distinte) — nel client legacy resa su due righe HTML separate, qui su una sola
/// riga con andata/ritorno affiancati (più leggibile, specialmente responsive).
/// </summary>
public class PopaLibertadoresResultRowViewModel
{
    public required string SeasonLabel { get; init; }

    /// <summary>Solo per l'ordinamento (LK_ORDINE), non mostrato in UI.</summary>
    public int SeasonOrder { get; init; }

    public TeamRef? Vincitore { get; init; }

    public TeamRef? FinalistaPerdente { get; init; }

    public string? RisultatoAndata { get; init; }

    public TeamRef? SedeAndata { get; init; }

    public string? SedeAndataStadio { get; init; }

    public string? RisultatoRitorno { get; init; }

    public TeamRef? SedeRitorno { get; init; }

    public string? SedeRitornoStadio { get; init; }
}
