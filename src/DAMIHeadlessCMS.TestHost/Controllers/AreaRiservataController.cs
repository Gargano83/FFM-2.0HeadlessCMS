using System.Security.Claims;
using DAMIHeadlessCMS.Admin.Ffm.Data;
using DAMIHeadlessCMS.Admin.Ffm.Models;
using DAMIHeadlessCMS.TestHost.Models.PublicSite;
using DAMIHeadlessCMS.TestHost.PublicSite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DAMIHeadlessCMS.TestHost.Controllers;

/// <summary>
/// Area Riservata (vedi docs/ROADMAP.md, migrazione pagine legacy). Checkpoint 2a: vista
/// squadra propria (info + rosa, sola lettura) e Altre Squadre — le azioni di modifica
/// (stato/mesi giocatore, rimozione, aggiunta svincolato) arrivano nel checkpoint 2b.
/// Riusa <see cref="IFfmSquadraRepository"/> (fase 7, già registrato in DI per il
/// backoffice): stessa fonte dati, nessuna nuova query. Autenticazione su WN_Utenti,
/// schema cookie separato da Identity (vedi <see cref="PublicAuthSchemes"/>).
/// </summary>
[Route("area-riservata")]
public class AreaRiservataController : Controller
{
    private readonly PublicUserRepository _users;
    private readonly IFfmSquadraRepository _squadre;

    public AreaRiservataController(PublicUserRepository users, IFfmSquadraRepository squadre)
    {
        _users = users;
        _squadre = squadre;
    }

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Index));
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _users.ValidateCredentialsAsync(model.Email, model.Password, ct);
        if (user is null)
        {
            // Stesso messaggio generico del legacy (non specifica se è l'email o la
            // password a essere sbagliata, per non facilitare l'enumerazione utenti).
            ModelState.AddModelError(string.Empty, "Email o password non corretti.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, $"{user.Nome} {user.Cognome}".Trim())
        };
        if (user.IdSquadra is int idSquadra)
        {
            claims.Add(new Claim("IdSquadra", idSquadra.ToString()));
        }

        var identity = new ClaimsIdentity(claims, PublicAuthSchemes.Cookie);
        await HttpContext.SignInAsync(PublicAuthSchemes.Cookie, new ClaimsPrincipal(identity), new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(10) : null
        });

        return model.ReturnUrl is not null && Url.IsLocalUrl(model.ReturnUrl)
            ? LocalRedirect(model.ReturnUrl)
            : RedirectToAction(nameof(Index));
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    [Authorize(AuthenticationSchemes = PublicAuthSchemes.Cookie)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(PublicAuthSchemes.Cookie);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("")]
    [Authorize(AuthenticationSchemes = PublicAuthSchemes.Cookie)]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var idSquadra = GetCurrentIdSquadra();
        if (idSquadra is null)
        {
            // Utente valido ma senza squadra associata (UT_Squadra nullo) — nessun
            // dato da mostrare, non un errore: pagina dedicata invece di un 404/500.
            return View("NessunaSquadra");
        }

        var model = await BuildTeamViewModelAsync(idSquadra.Value, isOwnTeam: true, ct);
        if (model is null)
        {
            return View("NessunaSquadra");
        }

        return View(model);
    }

    [HttpGet("altre-squadre")]
    [Authorize(AuthenticationSchemes = PublicAuthSchemes.Cookie)]
    public async Task<IActionResult> AltreSquadre(CancellationToken ct)
    {
        var squadre = await _squadre.GetSquadreListAsync(ct);
        return View(squadre);
    }

    [HttpGet("altre-squadre/{idSquadra:int}")]
    [Authorize(AuthenticationSchemes = PublicAuthSchemes.Cookie)]
    public async Task<IActionResult> AltraSquadra(int idSquadra, CancellationToken ct)
    {
        var model = await BuildTeamViewModelAsync(idSquadra, isOwnTeam: false, ct);
        if (model is null)
        {
            return NotFound();
        }

        // Stessa view della squadra propria: PuoModificare è già false (isOwnTeam: false),
        // quindi i link di modifica (checkpoint 2b) non compariranno.
        return View("Index", model);
    }

    private async Task<SquadraViewModel?> BuildTeamViewModelAsync(int idSquadra, bool isOwnTeam, CancellationToken ct)
    {
        var info = await _squadre.GetInfoSquadraAsync(idSquadra, ct);
        if (info is null)
        {
            return null;
        }

        var rosa = await _squadre.GetRosaAsync(idSquadra, ct);
        return new SquadraViewModel
        {
            Info = info,
            Rosa = rosa,
            PuoModificare = isOwnTeam && info.AbilitaModifica
        };
    }

    private int? GetCurrentIdSquadra()
    {
        var claim = User.FindFirst("IdSquadra")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}
