using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using DAMIHeadlessCMS.Admin.Ffm.Data;
using DAMIHeadlessCMS.Admin.Ffm.Models;
using DAMIHeadlessCMS.Data.Identity;

namespace DAMIHeadlessCMS.Admin.Ffm.Controllers;

/// <summary>
/// API REST usata dal componente Angular "Rosa Squadra". Sostituisce gli
/// endpoint legacy api/syncfusion/{aggiornainfosquadra,giocatorisquadra,
/// aggiungigiocatorepersquadra,aggiornagiocatorepersquadra,
/// dettagliogiocatorepersquadra,aggiornadettagliogiocatorepersquadra,
/// listagiocatori}. Riservata a CmsAdmin, come l'intero modulo FFM.
/// </summary>
[Route("dami/ffm/api/squadre")]
[Authorize(Policy = CmsAuthConstants.AdminPolicy)]
[ApiController]
public class FfmSquadreApiController : ControllerBase
{
    private readonly IFfmSquadraRepository _repository;
    private readonly IFfmUserResolver _userResolver;
    private readonly UserManager<CmsUser> _userManager;

    public FfmSquadreApiController(IFfmSquadraRepository repository, IFfmUserResolver userResolver, UserManager<CmsUser> userManager)
    {
        _repository = repository;
        _userResolver = userResolver;
        _userManager = userManager;
    }

    [HttpGet("")]
    public async Task<ActionResult<IReadOnlyList<SquadraListItemDto>>> GetSquadre(CancellationToken ct)
        => Ok(await _repository.GetSquadreListAsync(ct));

    [HttpGet("{idSquadra:int}/info")]
    public async Task<ActionResult<InfoSquadraDto>> GetInfo(int idSquadra, CancellationToken ct)
    {
        var info = await _repository.GetInfoSquadraAsync(idSquadra, ct);
        return info is null ? NotFound() : Ok(info);
    }

    [HttpGet("{idSquadra:int}/rosa")]
    public async Task<ActionResult<IReadOnlyList<GiocatoreSquadraDto>>> GetRosa(int idSquadra, CancellationToken ct)
        => Ok(await _repository.GetRosaAsync(idSquadra, ct));

    [HttpGet("{idSquadra:int}/rosa/{idGiocatore:int}")]
    public async Task<ActionResult<GiocatoreSquadraDto>> GetDettaglio(int idSquadra, int idGiocatore, CancellationToken ct)
    {
        var dettaglio = await _repository.GetDettaglioGiocatorePerSquadraAsync(idSquadra, idGiocatore, ct);
        return dettaglio is null ? NotFound() : Ok(dettaglio);
    }

    [HttpPut("{idSquadra:int}/rosa/{idGiocatore:int}")]
    public async Task<IActionResult> AggiornaDettaglio(int idSquadra, int idGiocatore, [FromBody] AggiornaRosaRequestDto request, CancellationToken ct)
    {
        var idUtente = await ResolveCurrentIdUtenteAsync(ct);
        await _repository.AggiornaDettaglioGiocatorePerSquadraAsync(idSquadra, idGiocatore, request.Mesi, request.Stato, idUtente, ct);
        return NoContent();
    }

    [HttpPost("{idSquadra:int}/rosa/{idGiocatore:int}")]
    public async Task<IActionResult> AggiungiGiocatore(int idSquadra, int idGiocatore, [FromBody] AggiungiGiocatoreRequestDto request, CancellationToken ct)
    {
        var idUtente = await ResolveCurrentIdUtenteAsync(ct);
        await _repository.AggiungiGiocatorePerSquadraAsync(idSquadra, idGiocatore, request.ValoreDiMercato, request.Stipendio, idUtente, ct);
        return NoContent();
    }

    [HttpDelete("{idSquadra:int}/rosa/{idGiocatore:int}")]
    public async Task<IActionResult> RimuoviGiocatore(int idSquadra, int idGiocatore, CancellationToken ct)
    {
        await _repository.EliminaGiocatorePerSquadraAsync(idSquadra, idGiocatore, ct);
        return NoContent();
    }

    [HttpGet("giocatori-svincolati")]
    public async Task<ActionResult<IReadOnlyList<GiocatoreSvincolatoDto>>> GetGiocatoriSvincolati(CancellationToken ct)
        => Ok(await _repository.GetGiocatoriSvincolatiAsync(ct));

    /// <summary>
    /// Risolve l'IdUtente legacy (WN_UTENTI.UT_ID) dall'email dell'utente CMS
    /// loggato. Null se non trovato: le scritture procedono comunque
    /// (IdUtente diventa NULL a database), per non bloccare l'operatività
    /// del backoffice per un utente non ancora mappato in WN_UTENTI.
    /// </summary>
    private async Task<int?> ResolveCurrentIdUtenteAsync(CancellationToken ct)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        return await _userResolver.ResolveIdUtenteAsync(currentUser?.Email, ct);
    }
}
