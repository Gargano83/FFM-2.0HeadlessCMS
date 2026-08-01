using DAMIHeadlessCMS.TestHost.PublicSite;
using Microsoft.AspNetCore.Mvc;

namespace DAMIHeadlessCMS.TestHost.Controllers;

/// <summary>
/// Pagina pubblica Comunicazioni (vedi docs/ROADMAP.md, migrazione pagine legacy).
/// Indice categorie/articoli e dettaglio articolo condividono lo stesso namespace di url
/// del legacy: "/comunicazioni/{slug}" prova prima come categoria, poi come articolo —
/// stessa logica di risoluzione di Blog.cs (categoria trovata per url, altrimenti
/// fallback a documento), qui senza il generico ContRouteHandler del legacy.
/// </summary>
[Route("comunicazioni")]
public class ComunicazioniController : Controller
{
    private readonly ComunicazioniDataService _data;

    public ComunicazioniController(ComunicazioniDataService data)
    {
        _data = data;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int pagina, CancellationToken ct)
    {
        var model = await _data.GetArticlesPageAsync(category: null, page: NormalizePage(pagina), ct);
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

    private static int NormalizePage(int page) => page < 1 ? 1 : page;
}
