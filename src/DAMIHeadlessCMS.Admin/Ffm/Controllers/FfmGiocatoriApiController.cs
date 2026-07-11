using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DAMIHeadlessCMS.Admin.Ffm.Data;
using DAMIHeadlessCMS.Admin.Ffm.Models;
using DAMIHeadlessCMS.Data.Identity;

namespace DAMIHeadlessCMS.Admin.Ffm.Controllers;

/// <summary>
/// API REST usata dal componente Angular "Database Giocatori" (FFM.Giocatori).
/// Sostituisce l'endpoint legacy api/syncfusion/* dedicato ai giocatori: stessa
/// logica di dominio (vedi FfmGiocatoriRepository), ma servita direttamente dal
/// backoffice DAMIHeadlessCMS invece che dal progetto host legacy. Le letture
/// (GET) sono accessibili anche a CmsOperator; le operazioni di scrittura
/// (Create/Update/Delete/Import) restano riservate a CmsAdmin (vedi gli
/// attributi [Authorize] espliciti sulle singole azioni) — è questo il vero
/// enforcement di sicurezza, la UI Angular in sola lettura è solo di supporto.
/// </summary>
[Route("dami/ffm/api/giocatori")]
[Authorize(Policy = CmsAuthConstants.FfmViewPolicy)]
[ApiController]
public class FfmGiocatoriApiController : ControllerBase
{
    private readonly IFfmGiocatoriRepository _repository;

    public FfmGiocatoriApiController(IFfmGiocatoriRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("")]
    public async Task<ActionResult<IReadOnlyList<GiocatoreDto>>> GetAll(CancellationToken ct)
        => Ok(await _repository.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GiocatoreDto>> GetById(int id, CancellationToken ct)
    {
        var giocatore = await _repository.GetByIdAsync(id, ct);
        return giocatore is null ? NotFound() : Ok(giocatore);
    }

    [HttpPost("")]
    [Authorize(Policy = CmsAuthConstants.AdminPolicy)]
    public async Task<ActionResult<GiocatoreDto>> Create([FromBody] GiocatoreDto giocatore, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var created = await _repository.CreateAsync(giocatore, ct);
        return Ok(created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = CmsAuthConstants.AdminPolicy)]
    public async Task<ActionResult<GiocatoreDto>> Update(int id, [FromBody] GiocatoreDto giocatore, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            await _repository.UpdateAsync(id, giocatore, ct);
            giocatore.Id = id;
            return Ok(giocatore);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = CmsAuthConstants.AdminPolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _repository.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Import massivo da Excel. ATTENZIONE: allinea FFM.Giocatori all'elenco
    /// fornito, eliminando i giocatori non presenti — vedi il commento su
    /// IFfmGiocatoriRepository.ImportAsync. La UI deve chiedere conferma
    /// esplicita all'utente prima di invocare questo endpoint.
    /// </summary>
    [HttpPost("import")]
    [Authorize(Policy = CmsAuthConstants.AdminPolicy)]
    public async Task<ActionResult<IReadOnlyList<GiocatoreDto>>> Import([FromBody] List<GiocatoreDto> giocatori, CancellationToken ct)
    {
        await _repository.ImportAsync(giocatori, ct);
        return Ok(await _repository.GetAllAsync(ct));
    }
}
