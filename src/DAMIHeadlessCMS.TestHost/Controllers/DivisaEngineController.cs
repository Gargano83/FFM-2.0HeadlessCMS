using DAMIHeadlessCMS.Admin.Ffm.Data;
using Microsoft.AspNetCore.Mvc;

namespace DAMIHeadlessCMS.TestHost.Controllers;

/// <summary>
/// Pagina di sola validazione tecnica per il motore di rendering Canvas 2D
/// della personalizzazione divisa squadra (fase 4 del piano — vedi
/// "piano-divisa-squadra.md" nel progetto Claude collegato). Non fa parte
/// del flusso utente reale di /area-riservata: nessuna autenticazione,
/// nessun salvataggio, nessun collegamento a una squadra specifica. Serve
/// solo a verificare che il motore JS (wwwroot/js/divisa/divisa-render-engine.js)
/// componga correttamente i template reali del catalogo con colori e testo
/// sponsor arbitrari, prima di scrivere gli endpoint host (fase 5) e
/// l'interfaccia definitiva integrata nel box "Squadra" (fase 6).
/// </summary>
[Route("dev/divisa-engine")]
public class DivisaEngineController : Controller
{
    private readonly IFfmDivisaTemplateRepository _templates;

    public DivisaEngineController(IFfmDivisaTemplateRepository templates)
    {
        _templates = templates;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var catalogo = await _templates.GetTemplateAttiviAsync(ct);
        return View(catalogo);
    }
}
