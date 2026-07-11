using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using DAMIHeadlessCMS.Admin.Ffm.Data;
using DAMIHeadlessCMS.Data;
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
[Authorize(Policy = CmsAuthConstants.FfmViewPolicy)]
public class FfmController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly IFfmSquadraRepository _squadraRepository;
    private readonly CmsDbContext _db;

    public FfmController(IConfiguration configuration, IFfmSquadraRepository squadraRepository, CmsDbContext db)
    {
        _configuration = configuration;
        _squadraRepository = squadraRepository;
        _db = db;
    }

    /// <summary>
    /// CmsOperator vede le pagine FFM in sola lettura: il flag viene passato
    /// ai Custom Element Angular (attributo HTML <c>read-only</c>) che
    /// disabilitano di conseguenza editing, toolbar e comandi della Grid.
    /// L'enforcement reale resta comunque lato server sulle API
    /// (FfmGiocatoriApiController/FfmSquadreApiController): questo flag è
    /// solo per l'esperienza utente, non l'unica barriera di sicurezza.
    /// </summary>
    private bool IsReadOnly => !User.IsInRole(CmsRoles.Admin);

    [HttpGet("giocatori")]
    public IActionResult Giocatori()
    {
        // Chiave di licenza community Syncfusion: letta da configurazione
        // dell'host (mai hardcoded nel bundle Angular, a differenza della
        // vecchia integrazione legacy che la registrava in main.ts).
        ViewBag.SyncfusionLicenseKey = _configuration["DAMIHeadlessCMS:Ffm:SyncfusionLicenseKey"];
        ViewBag.ReadOnly = IsReadOnly;
        return View();
    }

    /// <summary>
    /// Elenco squadre: il "dato anagrafico" (FFM.Squadre) è gestito dal CRUD
    /// generico del CMS una volta scaffoldata la tabella — questa pagina si
    /// limita a elencare le squadre e a linkare la vista Edit generica (se
    /// disponibile) più la pagina custom "Rosa" per ciascuna.
    /// </summary>
    [HttpGet("squadre")]
    public async Task<IActionResult> Squadre(CancellationToken ct)
    {
        var squadre = await _squadraRepository.GetSquadreListAsync(ct);

        // Guid dell'EntityDefinition di FFM.Squadre, se già scaffoldata: serve
        // per costruire il link "Modifica dati" verso il CRUD generico.
        ViewBag.SquadreEntityId = await _db.EntityDefinitions
            .Where(e => e.SchemaName == "FFM" && e.TableName == "Squadre")
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(ct);

        return View(squadre);
    }

    [HttpGet("squadre/{idSquadra:int}/rosa")]
    public IActionResult Rosa(int idSquadra)
    {
        ViewBag.SyncfusionLicenseKey = _configuration["DAMIHeadlessCMS:Ffm:SyncfusionLicenseKey"];
        ViewBag.IdSquadra = idSquadra;
        ViewBag.ReadOnly = IsReadOnly;
        return View();
    }
}
