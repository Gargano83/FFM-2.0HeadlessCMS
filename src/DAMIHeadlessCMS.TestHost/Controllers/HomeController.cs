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
        var hero = await LoadHeroAsync(ct);
        var teams = await LoadTeamsAsync(ct);

        return View(new HomeViewModel { Hero = hero, Teams = teams });
    }

    private async Task<HeroContentViewModel> LoadHeroAsync(CancellationToken ct)
    {
        var homepageDocumentId = _configuration.GetValue("PublicSite:HomepageDocumentId", 1);

        var entity = await _content.GetEntityAsync("dbo", "WN_Contenuti", ct);
        if (entity is null)
        {
            _logger.LogWarning(
                "WN_Contenuti non risulta ancora scaffoldata: la Homepage viene mostrata senza contenuto hero. " +
                "Scaffoldala da /dami/scaffolding per popolare questo blocco.");
            return HeroContentViewModel.NotFound;
        }

        var row = await _content.GetRowByIdAsync(entity, homepageDocumentId, ct);
        if (row is null)
        {
            _logger.LogWarning(
                "Nessuna riga trovata in WN_Contenuti per id={HomepageDocumentId} (PublicSite:HomepageDocumentId).",
                homepageDocumentId);
            return HeroContentViewModel.NotFound;
        }

        return new HeroContentViewModel
        {
            Found = true,
            Titolo = row.GetValueOrDefault("co_titolo") as string,
            Abstract = row.GetValueOrDefault("co_abstract") as string,
            Corpo = row.GetValueOrDefault("co_corpo") as string
        };
    }

    private async Task<IReadOnlyList<TeamLogoViewModel>> LoadTeamsAsync(CancellationToken ct)
    {
        var entity = await _content.GetEntityAsync("FFM", "Squadre", ct);
        if (entity is null)
        {
            _logger.LogWarning(
                "FFM.Squadre non risulta ancora scaffoldata: lo slider squadre viene omesso. " +
                "Scaffoldala da /dami/scaffolding per popolare questo blocco.");
            return [];
        }

        var baseUrl = _configuration["PublicSite:LegacyFileBaseUrl"] ?? string.Empty;
        var rows = await _content.GetAllRowsAsync(entity, ct: ct);

        return rows
            .Select(row => new TeamLogoViewModel
            {
                Nome = row.GetValueOrDefault("Nome") as string ?? string.Empty,
                LogoPath = ResolveLogoUrl(row.GetValueOrDefault("LogoStatistiche") as string, baseUrl)
            })
            .Where(t => !string.IsNullOrWhiteSpace(t.Nome))
            .ToList();
    }

    private static string? ResolveLogoUrl(string? relativePath, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        return relativePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               relativePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? relativePath
            : $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
