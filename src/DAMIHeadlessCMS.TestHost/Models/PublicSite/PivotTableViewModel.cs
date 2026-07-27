using DAMIHeadlessCMS.TestHost.PublicSite;

namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>
/// Pivot a colonne fisse per squadra (Allenatori/Presidenti) — a differenza dell'Albo
/// d'oro della Homepage, qui le colonne sono deliberatamente fisse (scelta esplicita di
/// Alessio, replica esatta della logica legacy: uno switch per un set noto di id squadra),
/// non generate dinamicamente dai dati.
/// </summary>
public class PivotTableViewModel
{
    public required IReadOnlyList<string> ColumnHeaders { get; init; }

    /// <summary>Intestazione di un'eventuale colonna extra prima del pivot (es. "Giornate" per Allenatori).</summary>
    public string? ExtraColumnHeader { get; init; }

    public required IReadOnlyList<PivotRowViewModel> Rows { get; init; }
}

/// <summary>Una riga (stagione) del pivot: Cells è allineato posizionalmente a PivotTableViewModel.ColumnHeaders.</summary>
public class PivotRowViewModel
{
    public required string SeasonLabel { get; init; }

    /// <summary>Solo per l'ordinamento (LK_ORDINE), non mostrato in UI.</summary>
    public int SeasonOrder { get; init; }

    public string? ExtraLabel { get; init; }

    public required IReadOnlyList<string> Cells { get; init; }
}

/// <summary>
/// Riga delle partecipazioni Campionato: un aggregato per squadra (titoli/podi/
/// partecipazioni), dinamico anche nel client legacy — nessun id hardcoded qui,
/// a differenza del pivot Allenatori/Presidenti sopra.
/// </summary>
public class PartecipazioniRowViewModel
{
    public TeamRef? Team { get; init; }

    public int Titoli { get; init; }

    public int SecondoPosto { get; init; }

    public int TerzoPosto { get; init; }

    public int Podi { get; init; }

    public int Partecipazioni { get; init; }
}
