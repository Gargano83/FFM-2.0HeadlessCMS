namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>
/// Pagina Statistiche, un blocco per checkpoint (vedi docs/ROADMAP.md). Le sezioni sono
/// esplicite (non un elenco generico) perché ogni "famiglia" di competizione ha una forma
/// diversa per i risultati — solo le tabelle Titoli sono davvero generiche.
/// </summary>
public class StatistichePageViewModel
{
    /// <summary>
    /// HTML del/dei blocco/i "html" di una CmsPage di supporto creata da backoffice
    /// (vedi StatisticheController), pensato per un'introduzione testuale/immagini
    /// sopra l'accordion — es. "Albi d'oro" nel legacy. Null se quella CmsPage non
    /// esiste o non è pubblicata: la pagina resta comunque perfettamente funzionante
    /// senza, semplicemente senza introduzione.
    /// </summary>
    public string? IntroHtml { get; init; }

    public IReadOnlyList<CampionatoResultRowViewModel> CampionatoRisultati { get; init; } = [];

    public TitleTableViewModel? CampionatoTitoli { get; init; }

    public IReadOnlyList<StandardCompetitionSectionViewModel> CompetizioniStandard { get; init; } = [];

    public IReadOnlyList<PopaLibertadoresResultRowViewModel> PopaLibertadoresRisultati { get; init; } = [];

    public TitleTableViewModel? PopaLibertadoresTitoli { get; init; }

    public IReadOnlyList<DualSourceCompetitionSectionViewModel> CompetizioniDoppiaSorgente { get; init; } = [];

    public IReadOnlyList<PartecipazioniRowViewModel> CampionatoPartecipazioni { get; init; } = [];

    public PivotTableViewModel? AllenatoriPivot { get; init; }

    public PivotTableViewModel? PresidentiPivot { get; init; }
}
