using DAMIHeadlessCMS.Data;
using DAMIHeadlessCMS.TestHost.PublicSite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DAMIHeadlessCMS.TestHost.ViewComponents;

/// <summary>
/// Renderizza il menu di navigazione principale, letto da CmsMenu/CmsMenuItem
/// (nativi del CMS — sostituiscono l'endpoint legacy /api/data/menu, la cui
/// logica ad albero per stringa "ca_ordine" non serve più: qui l'albero è
/// vero, via ParentId). Il nome del menu da leggere è una convenzione fissa
/// del progetto host (non configurabile), come deciso in fase di analisi.
/// </summary>
public class MainMenuViewComponent : ViewComponent
{
    /// <summary>Nome convenzionale del CmsMenu principale, da creare/gestire in /dami/menus.</summary>
    public const string MainMenuName = "main-nav";

    private readonly CmsDbContext _db;

    public MainMenuViewComponent(CmsDbContext db)
    {
        _db = db;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var menu = await _db.Menus
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Name == MainMenuName);

        var tree = menu is null
            ? []
            : MenuUrlResolver.BuildTree(menu.Items.ToList());

        return View(tree);
    }
}
