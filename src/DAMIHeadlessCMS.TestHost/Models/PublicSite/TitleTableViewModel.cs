namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>Tabella "Titoli" di una singola competizione (quante volte ogni squadra l'ha vinta).</summary>
public class TitleTableViewModel
{
    public required string CompetitionLabel { get; init; }

    public required IReadOnlyList<TitleRowViewModel> Rows { get; init; }
}

public class TitleRowViewModel
{
    public required string TeamName { get; init; }

    public string? LogoPath { get; init; }

    public int TitleCount { get; init; }

    /// <summary>Stagioni in cui la squadra ha vinto, già ordinate ed elencate come testo pronto per la UI.</summary>
    public required string Seasons { get; init; }
}
