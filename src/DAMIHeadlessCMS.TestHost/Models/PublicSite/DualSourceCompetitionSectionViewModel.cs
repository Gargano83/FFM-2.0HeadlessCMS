using DAMIHeadlessCMS.TestHost.PublicSite;

namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>
/// Riga risultati per la famiglia "doppia sorgente" — il vincitore proviene da una delle
/// due squadre "sorgente" (es. vincitrice Campionato vs vincitrice Poppa di Lega). Stessa
/// forma per SuperPoppa di Lega, SuperPoppa Europea, Poppa Intercontinentale (confermato
/// dalle query legacy: cambia solo il nome delle due colonne sorgente).
/// </summary>
public class DualSourceResultRowViewModel
{
    public required string SeasonLabel { get; init; }

    /// <summary>Solo per l'ordinamento (LK_ORDINE), non mostrato in UI.</summary>
    public int SeasonOrder { get; init; }

    /// <summary>
    /// Stagione non disputata (eccezione storica, solo per SuperPoppa di Lega 2019/2020 —
    /// vedi PublicSite:SuperpoppaDiLegaStagioneNonDisputata): tutti gli altri campi sono
    /// null quando true, la UI mostra "Non disputata" invece della riga normale.
    /// </summary>
    public bool NonDisputata { get; init; }

    public TeamRef? SourceA { get; init; }

    public TeamRef? SourceB { get; init; }

    public TeamRef? Vincitore { get; init; }

    public string? Risultato { get; init; }

    public TeamRef? SedeFinale { get; init; }

    public string? SedeFinaleStadio { get; init; }
}

/// <summary>Una sezione "doppia sorgente" della pagina Statistiche: risultati + titoli.</summary>
public class DualSourceCompetitionSectionViewModel
{
    public required string Label { get; init; }

    public required string Slug { get; init; }

    /// <summary>Etichetta della prima colonna sorgente (es. "Vincitore Campionato").</summary>
    public required string SourceALabel { get; init; }

    /// <summary>Etichetta della seconda colonna sorgente (es. "Vincitore Poppa di Lega").</summary>
    public required string SourceBLabel { get; init; }

    public IReadOnlyList<DualSourceResultRowViewModel> Risultati { get; init; } = [];

    public TitleTableViewModel? Titoli { get; init; }
}
