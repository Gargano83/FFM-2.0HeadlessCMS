using DAMIHeadlessCMS.Admin.Data;
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
/// modifica stato, rimozione, personalizzazione divisa (fase 5 del piano
/// "Personalizzazione divisa squadra", sviluppata e validata qui prima
/// dell'integrazione su FFM2.0Core — vedi <c>claude/piano-divisa-squadra.md</c>).
/// Consumata da fetch() in Views/AreaRiservata/Index.cshtml —
/// <see cref="AreaRiservataController"/> resta responsabile solo del
/// rendering iniziale delle pagine (login, caricamento server-side della
/// propria squadra o di un'altra per URL diretto/bookmark).
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
    private readonly IFfmDivisaTemplateRepository _divisaTemplate;
    private readonly IFfmDivisaRepository _divisa;
    private readonly IFileStorageProvider _fileStorage;
    private readonly AreaRiservataAuthorizationService _authorization;

    public AreaRiservataApiController(
        IFfmSquadraRepository squadre,
        IFfmGiocatoriRepository giocatori,
        IFfmDivisaTemplateRepository divisaTemplate,
        IFfmDivisaRepository divisa,
        IFileStorageProvider fileStorage,
        AreaRiservataAuthorizationService authorization)
    {
        _squadre = squadre;
        _giocatori = giocatori;
        _divisaTemplate = divisaTemplate;
        _divisa = divisa;
        _fileStorage = fileStorage;
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

        await _squadre.AggiornaDettaglioGiocatorePerSquadraAsync(idSquadra, idGiocatore, request.Stato, request.RuoliSpecifici, User.GetIdUtente(), ct);

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
    /// Catalogo template attivi + personalizzazione corrente della squadra
    /// (fase 5, "Personalizzazione divisa squadra") — sola lettura, nessuna
    /// verifica di CanEdit: consultare la divisa di un'altra squadra è sempre
    /// permesso, come per <see cref="GetSquadra"/>. <see cref="DivisaConfiguratoreDto.PuoModificare"/>
    /// riflette comunque la stessa regola di <see cref="AreaRiservataAuthorizationService.CanEdit"/>,
    /// così la UI (fase 6) sa se mostrare i controlli di modifica.
    /// </summary>
    [HttpGet("squadre/{idSquadra:int}/divisa")]
    public async Task<ActionResult<DivisaConfiguratoreDto>> GetDivisa(int idSquadra, CancellationToken ct)
    {
        var model = await BuildDivisaConfiguratoreAsync(idSquadra, ct);
        return model is null
            ? NotFound(new { error = "Squadra non trovata o nessun template divisa disponibile." })
            : Ok(model);
    }

    /// <summary>
    /// Crea/aggiorna la personalizzazione divisa (solo maglia) della squadra:
    /// template scelto, 3 colori, sponsor testuale, PNG "cotto" dal motore di
    /// rendering client-side (fase 4). Stessa autorizzazione di
    /// <see cref="AggiornaGiocatore"/> (<see cref="CheckCanEditAsync"/>) — non
    /// quella più stringente di <see cref="CheckCanAddOrRemoveAsync"/>,
    /// riservata alla composizione della rosa.
    /// </summary>
    [HttpPut("squadre/{idSquadra:int}/divisa")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<DivisaConfiguratoreDto>> AggiornaDivisa(
        int idSquadra, [FromBody] AggiornaDivisaApiRequestDto request, CancellationToken ct)
    {
        var authorizationError = await CheckCanEditAsync(idSquadra, ct);
        if (authorizationError is not null)
        {
            return authorizationError;
        }

        if (string.IsNullOrWhiteSpace(request.Colore1) || string.IsNullOrWhiteSpace(request.Colore2) || string.IsNullOrWhiteSpace(request.Colore3))
        {
            return BadRequest(new { error = "I tre colori della maglia sono obbligatori." });
        }

        var template = await _divisaTemplate.GetTemplateByIdAsync(request.IdTemplate, ct);
        if (template is null)
        {
            return BadRequest(new { error = "Template selezionato non valido." });
        }

        var attuale = await _divisa.GetDivisaAsync(idSquadra, ct);

        // Un template ritirato (Attivo = false) resta valido solo se è già
        // quello che la squadra aveva selezionato in precedenza: si può
        // continuare a usarlo, ma non passare a un template non più in
        // catalogo — stessa logica descritta su IFfmDivisaTemplateRepository.GetTemplateByIdAsync.
        if (!template.Attivo && (attuale is null || attuale.IdTemplate != request.IdTemplate))
        {
            return BadRequest(new { error = "Questo template non è più disponibile per nuove selezioni." });
        }

        var urlImmagine = attuale?.UrlImmagineGenerata;
        if (!string.IsNullOrWhiteSpace(request.ImmagineGenerataBase64))
        {
            if (!TryDecodeImmagineGenerata(request.ImmagineGenerataBase64, out var bytes))
            {
                return BadRequest(new { error = "Immagine divisa non valida: atteso un PNG come data URL." });
            }

            await using var stream = new MemoryStream(bytes);
            var nuovoUrl = await _fileStorage.SaveAsync(stream, "divisa.png", $"divisa/{idSquadra}", ct);

            // Nome file randomizzato ad ogni salvataggio (vedi IFileStorageProvider):
            // il vecchio PNG non serve più, va eliminato per non accumulare file
            // orfani ad ogni ri-personalizzazione.
            if (!string.IsNullOrWhiteSpace(urlImmagine))
            {
                await _fileStorage.DeleteAsync(urlImmagine, ct);
            }

            urlImmagine = nuovoUrl;
        }

        var dto = new AggiornaDivisaRequestDto
        {
            IdTemplate = request.IdTemplate,
            Colore1 = request.Colore1,
            Colore2 = request.Colore2,
            Colore3 = request.Colore3,
            TestoSponsor = request.TestoSponsor,
            ColoreTestoSponsor = request.ColoreTestoSponsor,
            ColoreContornoTestoSponsor = request.ColoreContornoTestoSponsor,
            ColoreSfondoTestoSponsor = request.ColoreSfondoTestoSponsor,
            PosizioneTestoSponsor = request.PosizioneTestoSponsor,
            FontTestoSponsor = request.FontTestoSponsor,
            ColoreOmbraTestoSponsor = request.ColoreOmbraTestoSponsor,
            DimensioneTestoSponsor = request.DimensioneTestoSponsor,
            AutoFitTestoSponsor = request.AutoFitTestoSponsor,
            LetteringAdArcoTestoSponsor = request.LetteringAdArcoTestoSponsor,
            UrlImmagineGenerata = urlImmagine
        };

        await _divisa.AggiornaDivisaAsync(idSquadra, dto, User.GetIdUtente(), ct);

        var model = await BuildDivisaConfiguratoreAsync(idSquadra, ct);
        return model is null ? NotFound() : Ok(model);
    }

    private const string DataUrlPngPrefix = "data:image/png;base64,";

    /// <summary>Accetta sia un data URL completo (<c>"data:image/png;base64,..."</c>) sia la sola stringa base64.</summary>
    private static bool TryDecodeImmagineGenerata(string dataUrl, out byte[] bytes)
    {
        var base64 = dataUrl.StartsWith(DataUrlPngPrefix, StringComparison.OrdinalIgnoreCase)
            ? dataUrl[DataUrlPngPrefix.Length..]
            : dataUrl;

        try
        {
            bytes = Convert.FromBase64String(base64);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    /// <summary>
    /// Null se la squadra non esiste, oppure se il catalogo non ha nessun
    /// template attivo da cui costruire un default (vedi
    /// <see cref="IFfmDivisaRepository.GetDivisaAsync"/>) — entrambi i casi
    /// limite riportati semplicemente come 404 dal chiamante.
    /// </summary>
    private async Task<DivisaConfiguratoreDto?> BuildDivisaConfiguratoreAsync(int idSquadra, CancellationToken ct)
    {
        var info = await _squadre.GetInfoSquadraAsync(idSquadra, ct);
        if (info is null)
        {
            return null;
        }

        var catalogo = await _divisaTemplate.GetTemplateAttiviAsync(ct);
        var divisa = await _divisa.GetDivisaAsync(idSquadra, ct);
        if (divisa is null)
        {
            return null;
        }

        var puoModificare = _authorization.CanEdit(idSquadra, User.GetIdSquadra(), User.GetIdUtente(), info.AbilitaModifica);

        return new DivisaConfiguratoreDto
        {
            CatalogoTemplate = catalogo,
            Divisa = divisa,
            PuoModificare = puoModificare
        };
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
