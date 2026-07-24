namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>
/// Pagina Statistiche, un blocco per checkpoint (vedi docs/ROADMAP.md). Le sezioni sono
/// esplicite (non un elenco generico) perché ogni "famiglia" di competizione ha una forma
/// diversa per i risultati — solo le tabelle Titoli sono davvero generiche.
/// </summary>
public class StatistichePageViewModel
{
    public IReadOnlyList<CampionatoResultRowViewModel> CampionatoRisultati { get; init; } = [];

    public TitleTableViewModel? CampionatoTitoli { get; init; }
}
