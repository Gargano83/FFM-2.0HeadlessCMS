using System.Security.Claims;
using DAMIHeadlessCMS.TestHost.Models.PublicSite;
using DAMIHeadlessCMS.TestHost.PublicSite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DAMIHeadlessCMS.TestHost.Controllers;

/// <summary>
/// Area Riservata (vedi docs/ROADMAP.md, migrazione pagine legacy). Checkpoint 1: solo
/// login/logout + pagina segnaposto post-login — la gestione rosa/giocatori (nel legacy
/// scritta a mano in client/, non con Syncfusion come nel backoffice) arriva in un
/// checkpoint successivo. Autenticazione su WN_Utenti, schema cookie separato da Identity
/// (vedi <see cref="PublicAuthSchemes"/>) — utenti pubblici e utenti backoffice sono due
/// popolazioni distinte, per esplicita indicazione di Alessio.
/// </summary>
[Route("area-riservata")]
public class AreaRiservataController : Controller
{
    private readonly PublicUserRepository _users;

    public AreaRiservataController(PublicUserRepository users)
    {
        _users = users;
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
    public IActionResult Index() => View();
}
