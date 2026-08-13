using DAMIHeadlessCMS.TestHost.PublicSite;
using Microsoft.AspNetCore.Mvc;

namespace DAMIHeadlessCMS.TestHost.Controllers;

/// <summary>
/// Pagina pubblica Comunicazioni (vedi docs/ROADMAP.md, migrazione pagine legacy).
/// Indice categorie/articoli e dettaglio articolo condividono lo stesso namespace di url
/// del legacy: "/comunicazioni/{slug}" prova prima come categoria, poi come articolo —
/// stessa logica di risoluzione di Blog.cs (categoria trovata per url, altrimenti
/// fallback a documento), qui senza il generico ContRouteHandler del legacy.
///
/// L'introduzione testuale/immagini sopra l'elenco NON è codificata qui: viene letta
/// da una CmsPage di supporto creata da backoffice (/dami/pages), slug
/// <see cref="IntroPageSlug"/> — stesso pattern già usato per Statistiche (README §6.1).
/// Mostrata solo nell'elenco non filtrato (nessuna categoria selezionata): filtrando per
/// categoria non avrebbe senso mostrare un'introduzione pensata per la sezione intera.
/// </summary>
[Route("comunicazioni")]
public class ComunicazioniController : Controller
{
    private const string IntroPageSlug = "comunicazioni-intro";

    private readonly ComunicazioniDataService _data;
    private readonly LegacyContentReader _content;

    public ComunicazioniController(ComunicazioniDataService data, LegacyContentReader content)
    {
        _data = data;
        _content = content;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int pagina, CancellationToken ct)
    {
        var model = await _data.GetArticlesPageAsync(category: null, page: NormalizePage(pagina), ct);
        model.IntroHtml = await GetIntroHtmlAsync(ct);
        return View(model);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Show(string slug, int pagina, CancellationToken ct)
    {
        var categories = await _data.GetCategoriesAsync(ct);
        var category = categories.FirstOrDefault(c => string.Equals(c.Slug, slug, StringComparison.OrdinalIgnoreCase));
        if (category is not null)
        {
            var listModel = await _data.GetArticlesPageAsync(category, NormalizePage(pagina), ct);
            return View("Index", listModel);
        }

        var article = await _data.TryGetArticleBySlugAsync(slug, ct);
        if (article is not null)
        {
            return View("Articolo", article);
        }

        return NotFound();
    }

    private async Task<string?> GetIntroHtmlAsync(CancellationToken ct)
    {
        var introContentJson = await _content.GetPageContentJsonAsync(IntroPageSlug, ct);
        return CmsPageContentParser.GetHtmlBlocksConcatenated(introContentJson);
    }

    private static int NormalizePage(int page) => page < 1 ? 1 : page;
}
