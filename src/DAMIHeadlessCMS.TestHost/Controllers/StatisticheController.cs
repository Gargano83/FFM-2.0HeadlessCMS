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

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var campionatoId = _data.GetCompetitionId("Campionato");

        var model = new StatistichePageViewModel
        {
            CampionatoRisultati = await _data.GetCampionatoResultsAsync(ct),
            CampionatoTitoli = campionatoId is int id
                ? await _data.BuildTitlesTableAsync(id, "Campionato", ct)
                : null
        };

        return View(model);
    }
}
