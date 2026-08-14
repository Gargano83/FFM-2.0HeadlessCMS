using DAMIHeadlessCMS.Admin.Ffm.Data;
using DAMIHeadlessCMS.Admin.Ffm.Models;
using DAMIHeadlessCMS.TestHost.Models.PublicSite;
using DAMIHeadlessCMS.TestHost.PublicSite;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DAMIHeadlessCMS.TestHost.Controllers;

/// <summary>
/// API JSON per le azioni "in-place" (senza reload di pagina) dell'Area
/// Riservata: cambio squadra dal selettore, ricerca/aggiunta giocatore,
/// modifica stato, rimozione. Consumata da fetch() in
/// Views/AreaRiservata/Index.cshtml — <see cref="AreaRiservataController"/>
/// resta responsabile solo del rendering iniziale delle pagine (login,
/// caricamento server-side della propria squadra o di un'altra per URL
/// diretto/bookmark).
///
/// Ogni azione di scrittura ri-verifica SEMPRE lato server, tramite
/// <see cref="AreaRiservataAuthorizationService"/>, che l'utente possa
/// modificare la squadra indicata nella route — mai fidandosi di un
/// eventuale stato "modificabile" mostrato in una risposta precedente:
/// squadra propria (con mercato aperto) oppure super-admin configurato
/// (PublicSite:AreaRiservataSuperAdminUserIds).
/// </summary>
[Route("area-riservata/api")]
[Authorize(AuthenticationSchemes = PublicAuthSchemes.Cookie)]
[ApiController]
public class AreaRiservataApiController : ControllerBase
{
    private readonly IFfmSquadraRepository _squadre;
    private readonly IFfmGiocatoriRepository _giocatori;
    private readonly AreaRiservataAuthorizationService _authorization;

    public AreaRiservataApiController(
        IFfmSquadraRepository squadre,
        IFfmGiocatoriRepository giocatori,
        AreaRiservataAuthorizationService authorization)
    {
        _squadre = squadre;
        _giocatori = giocatori;
        _authorization = authorization;
    }

    /// <summary>
    /// Elenco squadre "attive" per il selettore "vuoi consultare un'altra
    /// squadra?" — stesso filtro dell'endpoint legacy "api/club/squadreattive"
    /// (solo squadre con un presidente utente attivo associato). Non l'elenco
    /// completo usato dal backoffice: vedi
    /// <see cref="IFfmSquadraRepository.GetSquadreAttiveAsync"/>.
    /// </summary>
    [HttpGet("squadre")]
    public async Task<ActionResult<IReadOnlyList<SquadraListItemDto>>> GetSquadre(CancellationToken ct)
        => Ok(await _squadre.GetSquadreAttiveAsync(ct));

    /// <summary>
    /// Dati completi di una squadra (info, finanze, rosa, permesso di modifica per
    /// l'utente corrente) — stessa forma usata dal rendering server-side iniziale,
    /// così il JS che aggiorna la pagina dopo un cambio squadra dal selettore non
    /// deve gestire due formati diversi.
    /// </summary>
    [HttpGet("squadre/{idSquadra:int}")]
    public async Task<ActionResult<SquadraViewModel>> GetSquadra(int idSquadra, CancellationToken ct)
    {
        var model = await BuildSquadraViewModelAsync(idSquadra, ct);
        return model is null ? NotFound() : Ok(model);
    }

    /// <summary>
    /// Ricerca (autocompletamento) tra i giocatori svincolati per nome/cognome.
    /// Sola lettura, nessun controllo sulla squadra: la verifica del permesso di
    /// scrittura avviene solo al momento dell'aggiunta effettiva.
    /// </summary>
    [HttpGet("giocatori/cerca")]
    public async Task<ActionResult<IReadOnlyList<GiocatoreSvincolatoDto>>> CercaGiocatori([FromQuery] string? q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            return Ok(Array.Empty<GiocatoreSvincolatoDto>());
        }

        return Ok(await _squadre.CercaGiocatoriSvincolatiAsync(q.Trim(), limit: 15, ct));
    }

    /// <summary>
    /// Aggiunge un giocatore svincolato alla rosa, sempre con il suo Valore di
    /// mercato/Stipendio "di base" (FFM.Giocatori) — mai un valore fornito dal
    /// client: i risultati della ricerca li mostrano solo per informazione, non
    /// sono editabili in fase di aggiunta (stesso comportamento del legacy).
    /// </summary>
    [HttpPost("squadre/{idSquadra:int}/giocatori/{idGiocatore:int}")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<SquadraViewModel>> AggiungiGiocatore(int idSquadra, int idGiocatore, CancellationToken ct)
    {
        var authorizationError = await CheckCanAddOrRemoveAsync(idSquadra, ct);
        if (authorizationError is not null)
        {
            return authorizationError;
        }

        var giocatore = await _giocatori.GetByIdAsync(idGiocatore, ct);
        if (giocatore is null)
        {
            return NotFound(new { error = "Giocatore non trovato." });
        }

        await _squadre.AggiungiGiocatorePerSquadraAsync(
            idSquadra, idGiocatore, giocatore.ValoreDiMercato, giocatore.Stipendio, User.GetIdUtente(), ct);

        var model = await BuildSquadraViewModelAsync(idSquadra, ct);
        return model is null ? NotFound() : Ok(model);
    }

    /// <summary>Modifica lo stato (e implicitamente i mesi, invariati — solo lo stato è editabile dalla modale) di un giocatore in rosa.</summary>
    [HttpPut("squadre/{idSquadra:int}/giocatori/{idGiocatore:int}")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<SquadraViewModel>> AggiornaGiocatore(
        int idSquadra, int idGiocatore, [FromBody] AggiornaRosaRequestDto request, CancellationToken ct)
    {
        var authorizationError = await CheckCanEditAsync(idSquadra, ct);
        if (authorizationError is not null)
        {
            return authorizationError;
        }

        var giocatore = await _squadre.GetDettaglioGiocatorePerSquadraAsync(idSquadra, idGiocatore, ct);
        if (giocatore is null)
        {
            return NotFound(new { error = "Il giocatore non è nella rosa di questa squadra." });
        }

        await _squadre.AggiornaDettaglioGiocatorePerSquadraAsync(idSquadra, idGiocatore, request.Mesi, request.Stato, User.GetIdUtente(), ct);

        var model = await BuildSquadraViewModelAsync(idSquadra, ct);
        return model is null ? NotFound() : Ok(model);
    }

    [HttpDelete("squadre/{idSquadra:int}/giocatori/{idGiocatore:int}")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<SquadraViewModel>> RimuoviGiocatore(int idSquadra, int idGiocatore, CancellationToken ct)
    {
        var authorizationError = await CheckCanAddOrRemoveAsync(idSquadra, ct);
        if (authorizationError is not null)
        {
            return authorizationError;
        }

        var giocatore = await _squadre.GetDettaglioGiocatorePerSquadraAsync(idSquadra, idGiocatore, ct);
        if (giocatore is null)
        {
            return NotFound(new { error = "Il giocatore non è nella rosa di questa squadra." });
        }

        await _squadre.EliminaGiocatorePerSquadraAsync(idSquadra, idGiocatore, ct);

        var model = await BuildSquadraViewModelAsync(idSquadra, ct);
        return model is null ? NotFound() : Ok(model);
    }

    /// <summary>
    /// Null se la squadra non esiste. Autorizzazione ricalcolata sempre da capo
    /// (stessa logica del rendering server-side iniziale in AreaRiservataController):
    /// mai riusare un valore di PuoModificare calcolato altrove/in precedenza.
    /// </summary>
    private async Task<SquadraViewModel?> BuildSquadraViewModelAsync(int idSquadra, CancellationToken ct)
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
        var tutteLeSquadre = await _squadre.GetSquadreAttiveAsync(ct);

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

    /// <summary>Null (nessun errore) se l'utente può scrivere su questa squadra; altrimenti la risposta HTTP da restituire subito.</summary>
    private async Task<ActionResult?> CheckCanEditAsync(int idSquadra, CancellationToken ct)
    {
        var info = await _squadre.GetInfoSquadraAsync(idSquadra, ct);
        if (info is null)
        {
            return NotFound(new { error = "Squadra non trovata." });
        }

        var canEdit = _authorization.CanEdit(idSquadra, User.GetIdSquadra(), User.GetIdUtente(), info.AbilitaModifica);
        if (canEdit)
        {
            return null;
        }

        // Niente Forbid(PublicAuthSchemes.Cookie): triggererebbe il redirect
        // configurato come AccessDeniedPath per quello schema (la pagina di
        // login) — comportamento corretto per una pagina HTML, sbagliato per
        // una risposta JSON attesa da fetch(). Qui serve un 403 pulito.
        return StatusCode(StatusCodes.Status403Forbidden, new { error = "Non hai i permessi per modificare questa squadra." });
    }

    /// <summary>
    /// Come <see cref="CheckCanEditAsync"/> ma per aggiunta/rimozione giocatori,
    /// riservata ai soli super-admin (vedi
    /// <see cref="AreaRiservataAuthorizationService.CanAddOrRemove"/>) — anche
    /// un proprietario della squadra bersaglio, senza essere super-admin,
    /// riceve 403: nascondere i pulsanti in UI non è un controllo di sicurezza,
    /// va sempre ri-applicata qui la stessa regola mostrata al client.
    /// </summary>
    private async Task<ActionResult?> CheckCanAddOrRemoveAsync(int idSquadra, CancellationToken ct)
    {
        var info = await _squadre.GetInfoSquadraAsync(idSquadra, ct);
        if (info is null)
        {
            return NotFound(new { error = "Squadra non trovata." });
        }

        if (_authorization.CanAddOrRemove(User.GetIdUtente(), info.AbilitaModifica))
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, new { error = "Solo un amministratore può aggiungere o rimuovere giocatori dalla rosa." });
    }
}
