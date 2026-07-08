using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using DAMIHeadlessCMS.Data.Identity;

namespace DAMIHeadlessCMS.Admin.Ffm.Controllers;

/// <summary>
/// Pagine backoffice del modulo FFM: ospitano i componenti Angular/Syncfusion
/// come Custom Element indipendenti (vedi docs/ROADMAP.md, fase 7), montati
/// tramite un semplice tag HTML — non più iniettando l'intero index.html
/// compilato come faceva la vecchia integrazione legacy. Modulo opt-in:
/// registrato solo se l'host chiama AddDAMIHeadlessCMSFfm(...).
/// </summary>
[Route("dami/ffm")]
[Authorize(Policy = CmsAuthConstants.AdminPolicy)]
public class FfmController : Controller
{
    private readonly IConfiguration _configuration;

    public FfmController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("giocatori")]
    public IActionResult Giocatori()
    {
        // Chiave di licenza community Syncfusion: letta da configurazione
        // dell'host (mai hardcoded nel bundle Angular, a differenza della
        // vecchia integrazione legacy che la registrava in main.ts).
        ViewBag.SyncfusionLicenseKey = _configuration["DAMIHeadlessCMS:Ffm:SyncfusionLicenseKey"];
        return View();
    }
}
