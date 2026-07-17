using System.Diagnostics;
using DAMIHeadlessCMS.TestHost.Models;
using DAMIHeadlessCMS.TestHost.Models.PublicSite;
using DAMIHeadlessCMS.TestHost.PublicSite;
using Microsoft.AspNetCore.Mvc;

namespace DAMIHeadlessCMS.TestHost.Controllers;

/// <summary>
/// Pagina pubblica Homepage (vedi docs/ROADMAP.md, migrazione pagine legacy —
/// checkpoint "Menu + Hero"). Il blocco hero corrisponde a <c>Doc.Current</c>
/// nel progetto legacy: la riga di <c>WN_Contenuti</c> il cui id è configurato
/// in <c>PublicSite:HomepageDocumentId</c> (equivalente di WebConst.HOMEPAGE_ID).
/// </summary>
public class HomeController : Controller
{
    private readonly LegacyContentReader _content;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HomeController> _logger;

    public HomeController(LegacyContentReader content, IConfiguration configuration, ILogger<HomeController> logger)
    {
        _content = content;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var homepageDocumentId = _configuration.GetValue("PublicSite:HomepageDocumentId", 1);

        var entity = await _content.GetEntityAsync("dbo", "WN_Contenuti", ct);
        if (entity is null)
        {
            _logger.LogWarning(
                "WN_Contenuti non risulta ancora scaffoldata: la Homepage viene mostrata senza contenuto hero. " +
                "Scaffoldala da /dami/scaffolding per popolare questo blocco.");
            return View(HeroContentViewModel.NotFound);
        }

        var row = await _content.GetRowByIdAsync(entity, homepageDocumentId, ct);
        if (row is null)
        {
            _logger.LogWarning(
                "Nessuna riga trovata in WN_Contenuti per id={HomepageDocumentId} (PublicSite:HomepageDocumentId).",
                homepageDocumentId);
            return View(HeroContentViewModel.NotFound);
        }

        var hero = new HeroContentViewModel
        {
            Found = true,
            Titolo = row.GetValueOrDefault("co_titolo") as string,
            Abstract = row.GetValueOrDefault("co_abstract") as string,
            Corpo = row.GetValueOrDefault("co_corpo") as string
        };

        return View(hero);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
