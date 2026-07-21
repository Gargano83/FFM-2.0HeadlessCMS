namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>
/// Riepilogo statistiche ("Albo d'oro") pivotato per Stagione (righe) × Competizione
/// (colonne), entrambe generate dinamicamente dai dati trovati in
/// <c>FFM.RiepilogoStatistiche</c> — nessun id di competizione hardcoded, a differenza
/// del widget client-side legacy (9 colonne fisse per id di lookup 233–241).
/// </summary>
public class HallOfFameViewModel
{
    public required IReadOnlyList<string> CompetitionNames { get; init; }

    public required IReadOnlyList<HallOfFameRow> Rows { get; init; }
}

/// <summary>Una riga (stagione) della tabella: Teams è allineato posizionalmente a CompetitionNames.</summary>
public class HallOfFameRow
{
    public required string SeasonLabel { get; init; }

    public required IReadOnlyList<string?> Teams { get; init; }
}
