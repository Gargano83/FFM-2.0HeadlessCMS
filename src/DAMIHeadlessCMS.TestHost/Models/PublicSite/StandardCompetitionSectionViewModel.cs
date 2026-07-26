using DAMIHeadlessCMS.TestHost.PublicSite;

namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>
/// Riga risultati per la famiglia "standard" di competizioni (Vincitore/Finalista perdente/
/// Risultato/Sede finale) — stessa forma per Poppa Campioni, Coppa delle Poppe, Poppa di
/// Lega (verificato dalle query legacy: colonne identiche, cambia solo la tabella FFM.*).
/// </summary>
public class StandardResultRowViewModel
{
    public required string SeasonLabel { get; init; }

    /// <summary>Solo per l'ordinamento (LK_ORDINE), non mostrato in UI.</summary>
    public int SeasonOrder { get; init; }

    public TeamRef? Vincitore { get; init; }

    public TeamRef? FinalistaPerdente { get; init; }

    public string? Risultato { get; init; }

    public TeamRef? SedeFinale { get; init; }

    public string? SedeFinaleStadio { get; init; }
}

/// <summary>Una sezione della pagina Statistiche per una competizione "standard": risultati + titoli.</summary>
public class StandardCompetitionSectionViewModel
{
    public required string Label { get; init; }

    /// <summary>Usato per costruire id HTML univoci nell'accordion.</summary>
    public required string Slug { get; init; }

    public IReadOnlyList<StandardResultRowViewModel> Risultati { get; init; } = [];

    public TitleTableViewModel? Titoli { get; init; }
}
