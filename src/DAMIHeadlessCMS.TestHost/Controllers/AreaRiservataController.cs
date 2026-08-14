using System.Security.Claims;
using DAMIHeadlessCMS.Admin.Ffm.Data;
using DAMIHeadlessCMS.TestHost.Models.PublicSite;
using DAMIHeadlessCMS.TestHost.PublicSite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DAMIHeadlessCMS.TestHost.Controllers;

/// <summary>
/// Area Riservata (vedi docs/ROADMAP.md, migrazione pagine legacy). Responsabile
/// solo del rendering iniziale delle pagine (login/logout, propria squadra o
/// un'altra per url diretto/bookmark) — le azioni di scrittura sulla rosa
/// (aggiungi/modifica/rimuovi giocatore) e il cambio squadra "in-place" dal
/// selettore vivono in <see cref="AreaRiservataApiController"/>, consumata via
/// fetch() da Views/AreaRiservata/Index.cshtml senza reload di pagina — stesso
/// modello dati (<see cref="SquadraViewModel"/>) sia al primo caricamento
/// server-side sia negli aggiornamenti successivi via API, per evitare due
/// formati diversi da tenere allineati.
///
/// Riusa <see cref="IFfmSquadraRepository"/> (fase 7, già registrato in DI per
/// il backoffice): stessa fonte dati, nessuna nuova query. Autenticazione su
/// WN_Utenti, schema cookie separato da Identity (vedi
/// <see cref="PublicAuthSchemes"/>).
/// </summary>
[Route("area-riservata")]
public class AreaRiservataController : Controller
{
    private readonly PublicUserRepository _users;
    private readonly IFfmSquadraRepository _squadre;
    private readonly AreaRiservataAuthorizationService _authorization;

    public AreaRiservataController(PublicUserRepository users, IFfmSquadraRepository squadre, AreaRiservataAuthorizationService authorization)
    {
        _users = users;
        _squadre = squadre;
        _authorization = authorization;
    }

    [HttpGet("login")]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        // Va interrogato esplicitamente lo schema PublicUser: User.Identity.IsAuthenticated
        // rifletterebbe invece lo schema di autenticazione di DEFAULT dell'applicazione
        // (Identity/backoffice, popolato da app.UseAuthentication() indipendentemente da
        // questo schema secondario) — con l'effetto che un amministratore loggato su
        // /dami nello stesso browser verrebbe considerato "già loggato" anche qui, pur
        // non avendo mai fatto login nell'area riservata: redirect a Index(), che a sua
        // volta richiede lo schema PublicUser e fallisce, tornando su questa stessa
        // azione — un loop di redirect infinito (ERR_TOO_MANY_REDIRECTS).
        var publicAuth = await HttpContext.AuthenticateAsync(PublicAuthSchemes.Cookie);
        if (publicAuth.Succeeded)
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
        var idSquadra = User.GetIdSquadra();
        if (idSquadra is null)
        {
            // Utente valido ma senza squadra associata (UT_Squadra nullo) — nessun
            // dato da mostrare, non un errore: pagina dedicata invece di un 404/500.
            return View("NessunaSquadra");
        }

        var model = await BuildTeamViewModelAsync(idSquadra.Value, ct);
        if (model is null)
        {
            return View("NessunaSquadra");
        }

        ViewBag.IdSquadraUtenteCorrente = idSquadra;
        return View(model);
    }

    /// <summary>
    /// Ingresso diretto/bookmark su un'altra squadra (es. link condiviso, o URL
    /// aggiornata via history.pushState dal selettore lato client — vedi
    /// Index.cshtml). Stessa view della squadra propria: se l'utente non ha i
    /// permessi di modifica su questa squadra (non è la propria e non è
    /// super-admin, o il mercato è chiuso), PuoModificare è semplicemente false
    /// e i controlli di modifica non compaiono — nessun redirect o errore, dato
    /// che consultare un'altra squadra in sola lettura è sempre permesso.
    /// </summary>
    [HttpGet("altre-squadre/{idSquadra:int}")]
    [Authorize(AuthenticationSchemes = PublicAuthSchemes.Cookie)]
    public async Task<IActionResult> AltraSquadra(int idSquadra, CancellationToken ct)
    {
        var model = await BuildTeamViewModelAsync(idSquadra, ct);
        if (model is null)
        {
            return NotFound();
        }

        ViewBag.IdSquadraUtenteCorrente = User.GetIdSquadra();
        return View("Index", model);
    }

    private async Task<SquadraViewModel?> BuildTeamViewModelAsync(int idSquadra, CancellationToken ct)
    {
        var info = await _squadre.GetInfoSquadraAsync(idSquadra, ct);
        if (info is null)
        {
            return null;
        }

        var idSquadraUtente = User.GetIdSquadra();
        var idUtente = User.GetIdUtente();
        var puoModificare = _authorization.CanEdit(idSquadra, idSquadraUtente, idUtente, info.AbilitaModifica);
        var puoAggiungereRimuovere = _authorization.CanAddOrRemove(idUtente, info.AbilitaModifica);

        var rosa = await _squadre.GetRosaAsync(idSquadra, ct);
        var tutteLeSquadre = await _squadre.GetSquadreListAsync(ct);

        return new SquadraViewModel
        {
            Info = info,
            Rosa = rosa,
            PuoModificare = puoModificare,
            PuoAggiungereRimuovere = puoAggiungereRimuovere,
            IsSuperAdminOverride = puoModificare && idSquadra != idSquadraUtente,
            TutteLeSquadre = tutteLeSquadre
        };
    }
}
