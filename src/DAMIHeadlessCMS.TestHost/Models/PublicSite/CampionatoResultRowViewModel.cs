using DAMIHeadlessCMS.TestHost.PublicSite;

namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>Una riga (stagione) dei risultati del Campionato: solo il podio.</summary>
public class CampionatoResultRowViewModel
{
    public required string SeasonLabel { get; init; }

    /// <summary>Solo per l'ordinamento (LK_ORDINE), non mostrato in UI.</summary>
    public int SeasonOrder { get; init; }

    public TeamRef? Primo { get; init; }

    public TeamRef? Secondo { get; init; }

    public TeamRef? Terzo { get; init; }
}
