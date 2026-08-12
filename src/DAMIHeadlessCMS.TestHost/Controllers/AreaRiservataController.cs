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
/// Area Riservata (vedi docs/ROADMAP.md, migrazione pagine legacy). Checkpoint 2a+2b
/// completati: vista squadra propria/Altre Squadre (sola lettura) e azioni di modifica
/// (stato/mesi giocatore, rimozione dalla rosa, aggiunta svincolato). Riusa
/// <see cref="IFfmSquadraRepository"/> (fase 7, già registrato in DI per il backoffice):
/// stessa fonte dati, nessuna nuova query. Autenticazione su WN_Utenti, schema cookie
/// separato da Identity (vedi <see cref="PublicAuthSchemes"/>). Ogni azione di scrittura
/// verifica sempre che il giocatore appartenga alla squadra del claim IdSquadra
/// dell'utente autenticato, mai fidandosi del solo id nella route.
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

    [HttpGet("giocatori/{idGiocatore:int}/modifica")]
    [Authorize(AuthenticationSchemes = PublicAuthSchemes.Cookie)]
    public async Task<IActionResult> ModificaGiocatore(int idGiocatore, CancellationToken ct)
    {
        var idSquadra = GetCurrentIdSquadra();
        if (idSquadra is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var info = await _squadre.GetInfoSquadraAsync(idSquadra.Value, ct);
        if (info is null || !info.AbilitaModifica)
        {
            TempData["Errore"] = "Le modifiche alla rosa non sono al momento consentite.";
            return RedirectToAction(nameof(Index));
        }

        // Non ci si fida del solo id nella route: il giocatore deve risultare
        // davvero nella rosa della PROPRIA squadra (claim IdSquadra), mai in
        // quella indicata da un eventuale parametro esterno.
        var giocatore = await _squadre.GetDettaglioGiocatorePerSquadraAsync(idSquadra.Value, idGiocatore, ct);
        if (giocatore is null)
        {
            return NotFound();
        }

        ViewBag.StatiGiocatore = StatiGiocatore;
        return View(new ModificaGiocatoreViewModel
        {
            IdGiocatore = giocatore.Id,
            NomeCompleto = giocatore.NomeCompleto,
            Ruolo = giocatore.Ruolo,
            Mesi = giocatore.Mesi,
            Stato = giocatore.Stato
        });
    }

    [HttpPost("giocatori/{idGiocatore:int}/modifica")]
    [ValidateAntiForgeryToken]
    [Authorize(AuthenticationSchemes = PublicAuthSchemes.Cookie)]
    public async Task<IActionResult> ModificaGiocatore(int idGiocatore, ModificaGiocatoreViewModel model, CancellationToken ct)
    {
        var idSquadra = GetCurrentIdSquadra();
        if (idSquadra is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var info = await _squadre.GetInfoSquadraAsync(idSquadra.Value, ct);
        var giocatore = await _squadre.GetDettaglioGiocatorePerSquadraAsync(idSquadra.Value, idGiocatore, ct);
        if (giocatore is null)
        {
            return NotFound();
        }

        if (info is null || !info.AbilitaModifica)
        {
            ModelState.AddModelError(string.Empty, "Le modifiche alla rosa non sono al momento consentite.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.StatiGiocatore = StatiGiocatore;
            model.NomeCompleto = giocatore.NomeCompleto;
            model.Ruolo = giocatore.Ruolo;
            return View(model);
        }

        await _squadre.AggiornaDettaglioGiocatorePerSquadraAsync(
            idSquadra.Value, idGiocatore, model.Mesi, model.Stato, GetCurrentIdUtente(), ct);

        TempData["Messaggio"] = "Giocatore aggiornato.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("giocatori/{idGiocatore:int}/rimuovi")]
    [ValidateAntiForgeryToken]
    [Authorize(AuthenticationSchemes = PublicAuthSchemes.Cookie)]
    public async Task<IActionResult> RimuoviGiocatore(int idGiocatore, CancellationToken ct)
    {
        var idSquadra = GetCurrentIdSquadra();
        if (idSquadra is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var info = await _squadre.GetInfoSquadraAsync(idSquadra.Value, ct);
        if (info is null || !info.AbilitaModifica)
        {
            TempData["Errore"] = "Le modifiche alla rosa non sono al momento consentite.";
            return RedirectToAction(nameof(Index));
        }

        var giocatore = await _squadre.GetDettaglioGiocatorePerSquadraAsync(idSquadra.Value, idGiocatore, ct);
        if (giocatore is null)
        {
            return NotFound();
        }

        await _squadre.EliminaGiocatorePerSquadraAsync(idSquadra.Value, idGiocatore, ct);

        TempData["Messaggio"] = "Giocatore rimosso dalla rosa.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("giocatori/aggiungi")]
    [Authorize(AuthenticationSchemes = PublicAuthSchemes.Cookie)]
    public async Task<IActionResult> AggiungiGiocatore(CancellationToken ct)
    {
        var idSquadra = GetCurrentIdSquadra();
        if (idSquadra is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var info = await _squadre.GetInfoSquadraAsync(idSquadra.Value, ct);
        if (info is null || !info.AbilitaModifica)
        {
            TempData["Errore"] = "Le modifiche alla rosa non sono al momento consentite.";
            return RedirectToAction(nameof(Index));
        }

        var disponibili = await _squadre.GetGiocatoriSvincolatiAsync(ct);
        return View(new AggiungiGiocatoreViewModel { Disponibili = disponibili });
    }

    [HttpPost("giocatori/aggiungi")]
    [ValidateAntiForgeryToken]
    [Authorize(AuthenticationSchemes = PublicAuthSchemes.Cookie)]
    public async Task<IActionResult> AggiungiGiocatore(AggiungiGiocatoreViewModel model, CancellationToken ct)
    {
        var idSquadra = GetCurrentIdSquadra();
        if (idSquadra is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var info = await _squadre.GetInfoSquadraAsync(idSquadra.Value, ct);
        if (info is null || !info.AbilitaModifica)
        {
            ModelState.AddModelError(string.Empty, "Le modifiche alla rosa non sono al momento consentite.");
        }

        if (!ModelState.IsValid)
        {
            model.Disponibili = await _squadre.GetGiocatoriSvincolatiAsync(ct);
            return View(model);
        }

        await _squadre.AggiungiGiocatorePerSquadraAsync(
            idSquadra.Value, model.IdGiocatoreSelezionato!.Value, model.ValoreDiMercato, model.Stipendio, GetCurrentIdUtente(), ct);

        TempData["Messaggio"] = "Giocatore aggiunto alla rosa.";
        return RedirectToAction(nameof(Index));
    }

    private static readonly string[] StatiGiocatore = ["", "Lista A", "Lista A (Pr)", "No Serie A", "In prestito", "Fuori Rosa"];

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

    private int? GetCurrentIdUtente()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}
