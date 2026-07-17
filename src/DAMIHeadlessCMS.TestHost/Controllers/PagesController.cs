using DAMIHeadlessCMS.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DAMIHeadlessCMS.TestHost.Controllers;

/// <summary>
/// Rendering pubblico di una <see cref="Core.Entities.CmsPage"/> nativa, raggiunta
/// tramite la rotta convenzionale "{slug}" (vedi Program.cs). Riservato a contenuti
/// creati direttamente da backoffice (non provenienti dal legacy, che invece vengono
/// letti dalle tabelle scaffoldate — vedi HomeController/LegacyContentReader).
/// Interpreta solo blocchi di tipo "html"; gli altri tipi (component/entityList,
/// vedi CmsPage.ContentJson) verranno gestiti quando servirà davvero un caso d'uso.
/// </summary>
public class PagesController : Controller
{
    private readonly CmsDbContext _db;

    public PagesController(CmsDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Show(string slug, CancellationToken ct)
    {
        var page = await _db.Pages
            .Where(p => p.Slug == slug && p.IsPublished)
            .FirstOrDefaultAsync(ct);

        if (page is null)
        {
            return NotFound();
        }

        return View(page);
    }
}
