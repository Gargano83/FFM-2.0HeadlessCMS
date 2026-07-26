using DAMIHeadlessCMS.TestHost.Models.PublicSite;
using DAMIHeadlessCMS.TestHost.PublicSite;
using Microsoft.AspNetCore.Mvc;

namespace DAMIHeadlessCMS.TestHost.Controllers;

/// <summary>
/// Pagina pubblica Statistiche (vedi docs/ROADMAP.md, migrazione pagine legacy — fase
/// dedicata, sviluppata per checkpoint data la dimensione: ~20 sezioni nel legacy).
/// Checkpoint 1/4: componente Titoli condiviso (riusabile da tutte le prossime sezioni,
/// derivato da FFM.RiepilogoStatistiche già scaffoldata) + risultati Campionato.
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

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var campionatoId = _data.GetCompetitionId("Campionato");

        var model = new StatistichePageViewModel
        {
            CampionatoRisultati = await _data.GetCampionatoResultsAsync(ct),
            CampionatoTitoli = campionatoId is int id
                ? await _data.BuildTitlesTableAsync(id, "Campionato", ct)
                : null,
            CompetizioniStandard = await BuildStandardCompetitionSectionsAsync(ct)
        };

        return View(model);
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
