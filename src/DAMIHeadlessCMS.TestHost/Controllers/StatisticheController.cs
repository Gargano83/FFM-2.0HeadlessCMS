using DAMIHeadlessCMS.TestHost.Models.PublicSite;
using DAMIHeadlessCMS.TestHost.PublicSite;
using Microsoft.AspNetCore.Mvc;

namespace DAMIHeadlessCMS.TestHost.Controllers;

/// <summary>
/// Pagina pubblica Statistiche (vedi docs/ROADMAP.md, migrazione pagine legacy — fase
/// 18, sviluppata per checkpoint data la dimensione: ~20 sezioni nel legacy). Tutti e 4
/// i checkpoint completati: Titoli condiviso + Campionato, famiglia standard, famiglia
/// speciale, partecipazioni Campionato + pivot Allenatori/Presidenti.
/// </summary>
public class StatisticheController : Controller
{
    private readonly StatisticheDataService _data;

    public StatisticheController(StatisticheDataService data)
    {
        _data = data;
    }

    // Nome tabella FFM.*, etichetta in pagina, chiave in PublicSite:Competizioni: stessa
    // forma per tutt'e tre (verificato dalle query legacy), un solo metodo generico le serve.
    private static readonly (string Table, string Label, string ConfigKey, string Slug)[] StandardCompetitions =
    [
        ("PoppaCampioniStatistiche", "Poppa Campioni", "PoppaCampioni", "poppa-campioni"),
        ("CoppaDellePoppeStatistiche", "Coppa delle Poppe", "CoppaDellePoppe", "coppa-dellepoppe"),
        ("PoppaDiLegaStatistiche", "Poppa di Lega", "PoppaDiLega", "poppa-dilega")
    ];

    // Nome tabella FFM.*, etichette delle due colonne sorgente, chiave in
    // PublicSite:Competizioni, slug per l'accordion: stessa forma per tutt'e tre
    // (confermato dalle query legacy), un solo metodo generico le serve.
    private static readonly (string Table, string Label, string SourceAColumn, string SourceALabel, string SourceBColumn, string SourceBLabel, string ConfigKey, string Slug, bool HasNonDisputataException)[] DualSourceCompetitions =
    [
        ("SuperpoppaDiLegaStatistiche", "SuperPoppa di Lega", "VincitoreCampionato", "Vincitore Campionato", "VincitorePoppaDiLega", "Vincitore Poppa di Lega", "SuperpoppaDiLega", "superpoppa-dilega", true),
        ("SuperpoppaEuropeaStatistiche", "SuperPoppa Europea", "VincitorePoppaDeiCampioni", "Vincitore Poppa dei Campioni", "VincitorePoppaUefa", "Vincitore Poppa Uefa", "SuperpoppaEuropea", "superpoppa-europea", false),
        ("PoppaIntercontinentaleStatistiche", "Poppa Intercontinentale", "VincitoreCoppaDellePoppe", "Vincitore Coppa delle Poppe", "VincitorePopaLibertadores", "Vincitore Popa Libertadores", "PoppaIntercontinentale", "poppa-intercontinentale", false)
    ];

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var campionatoId = _data.GetCompetitionId("Campionato");
        var popaLibertadoresId = _data.GetCompetitionId("PopaLibertadores");

        var model = new StatistichePageViewModel
        {
            CampionatoRisultati = await _data.GetCampionatoResultsAsync(ct),
            CampionatoTitoli = campionatoId is int id
                ? await _data.BuildTitlesTableAsync(id, "Campionato", ct)
                : null,
            CompetizioniStandard = await BuildStandardCompetitionSectionsAsync(ct),
            PopaLibertadoresRisultati = await _data.GetPopaLibertadoresResultsAsync(ct),
            PopaLibertadoresTitoli = popaLibertadoresId is int popaId
                ? await _data.BuildTitlesTableAsync(popaId, "Popa Libertadores", ct)
                : null,
            CompetizioniDoppiaSorgente = await BuildDualSourceCompetitionSectionsAsync(ct),
            CampionatoPartecipazioni = await _data.GetCampionatoPartecipazioniAsync(ct),
            AllenatoriPivot = await _data.GetAllenatoriPivotAsync(ct),
            PresidentiPivot = await _data.GetPresidentiPivotAsync(ct)
        };

        return View(model);
    }

    private async Task<IReadOnlyList<DualSourceCompetitionSectionViewModel>> BuildDualSourceCompetitionSectionsAsync(CancellationToken ct)
    {
        var nonDisputataLabel = _data.GetNonDisputataSeasonLabel();
        var sections = new List<DualSourceCompetitionSectionViewModel>();

        foreach (var (table, label, sourceAColumn, sourceALabel, sourceBColumn, sourceBLabel, configKey, slug, hasException) in DualSourceCompetitions)
        {
            var risultati = await _data.GetDualSourceCompetitionResultsAsync(
                table, sourceAColumn, sourceBColumn, hasException ? nonDisputataLabel : null, ct);
            var competitionId = _data.GetCompetitionId(configKey);
            var titoli = competitionId is int id ? await _data.BuildTitlesTableAsync(id, label, ct) : null;

            if (risultati.Count == 0 && titoli is null)
            {
                continue;
            }

            sections.Add(new DualSourceCompetitionSectionViewModel
            {
                Label = label,
                Slug = slug,
                SourceALabel = sourceALabel,
                SourceBLabel = sourceBLabel,
                Risultati = risultati,
                Titoli = titoli
            });
        }

        return sections;
    }

    private async Task<IReadOnlyList<StandardCompetitionSectionViewModel>> BuildStandardCompetitionSectionsAsync(CancellationToken ct)
    {
        var sections = new List<StandardCompetitionSectionViewModel>();

        foreach (var (table, label, configKey, slug) in StandardCompetitions)
        {
            var risultati = await _data.GetStandardCompetitionResultsAsync(table, ct);
            var competitionId = _data.GetCompetitionId(configKey);
            var titoli = competitionId is int id ? await _data.BuildTitlesTableAsync(id, label, ct) : null;

            if (risultati.Count == 0 && titoli is null)
            {
                continue;
            }

            sections.Add(new StandardCompetitionSectionViewModel
            {
                Label = label,
                Slug = slug,
                Risultati = risultati,
                Titoli = titoli
            });
        }

        return sections;
    }
}
