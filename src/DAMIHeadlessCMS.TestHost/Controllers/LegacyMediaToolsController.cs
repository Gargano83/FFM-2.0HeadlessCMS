using DAMIHeadlessCMS.Data.Identity;
using DAMIHeadlessCMS.TestHost.PublicSite;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DAMIHeadlessCMS.TestHost.Controllers;

/// <summary>
/// Strumento di manutenzione one-off, non collegato al menu del backoffice
/// (raggiungibile solo digitando l'url, come le CmsPage di supporto tipo
/// "statistiche-intro"): migra i loghi squadra dal sito legacy
/// (<c>PublicSite:LegacyFileBaseUrl</c>) allo storage locale di quest'host —
/// vedi <see cref="LegacyMediaMigrationService"/>. Riservato a CmsAdmin,
/// stessa policy usata per le operazioni strutturali del backoffice
/// (scaffolding, gestione utenti).
/// </summary>
[Route("dami/tools/legacy-media")]
[Authorize(Policy = CmsAuthConstants.AdminPolicy)]
public class LegacyMediaToolsController : Controller
{
    private readonly LegacyMediaMigrationService _migration;

    public LegacyMediaToolsController(LegacyMediaMigrationService migration)
    {
        _migration = migration;
    }

    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpPost("migra-loghi-squadre")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MigraLoghiSquadre(CancellationToken ct)
    {
        var result = await _migration.MigrateSquadreLogosAsync(ct);
        return View("Index", result);
    }
}
