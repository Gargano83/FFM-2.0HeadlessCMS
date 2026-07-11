using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DAMIHeadlessCMS.Admin.Utilities;
using DAMIHeadlessCMS.Admin.ViewModels;
using DAMIHeadlessCMS.Core.Entities;
using DAMIHeadlessCMS.Core.Enums;
using DAMIHeadlessCMS.Data;
using DAMIHeadlessCMS.Data.Identity;

namespace DAMIHeadlessCMS.Admin.Controllers;

/// <summary>
/// CRUD delle pagine custom del CMS (contenuto a blocchi in CmsPage.ContentJson).
/// Il rendering front-end dei blocchi è responsabilità dell'app host: qui si
/// gestisce solo la composizione/editing dei blocchi in backoffice.
/// </summary>
[Route("dami/pages")]
[Authorize(Policy = CmsAuthConstants.EditorPolicy)]
public class PagesController : Controller
{
    private readonly CmsDbContext _db;

    public PagesController(CmsDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var pages = await _db.Pages.ToListAsync(ct);
        var byId = pages.ToDictionary(p => p.Id);

        var items = pages
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Title)
            .Select(p => new PageListItemViewModel
            {
                Id = p.Id,
                Slug = p.Slug,
                Title = p.Title,
                ParentId = p.ParentId,
                ParentTitle = p.ParentId.HasValue && byId.TryGetValue(p.ParentId.Value, out var parent)
                    ? parent.Title
                    : null,
                IsPublished = p.IsPublished,
                SortOrder = p.SortOrder
            })
            .ToList();

        return View(items);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var model = new PageFormViewModel();
        await PopulateOptionsAsync(model, excludePageId: null, ct);
        return View(model);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PageFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, excludePageId: null, ct);
            return View(model);
        }

        if (await _db.Pages.AnyAsync(p => p.Slug == model.Slug, ct))
        {
            ModelState.AddModelError(nameof(model.Slug), "Esiste già una pagina con questo slug.");
            await PopulateOptionsAsync(model, excludePageId: null, ct);
            return View(model);
        }

        var conflictingMenuPath = await FindConflictingInternalMenuPathAsync(model.Slug, ct);
        if (conflictingMenuPath is not null)
        {
            ModelState.AddModelError(nameof(model.Slug),
                $"Il percorso '{conflictingMenuPath}' è già usato da una voce di menu di tipo 'URL esterno'. Cambia slug o aggiorna quella voce di menu.");
            await PopulateOptionsAsync(model, excludePageId: null, ct);
            return View(model);
        }

        var page = new CmsPage
        {
            Id = Guid.NewGuid(),
            Slug = model.Slug,
            Title = model.Title,
            ParentId = model.ParentId,
            IsPublished = model.IsPublished,
            SortOrder = model.SortOrder,
            ContentJson = string.IsNullOrWhiteSpace(model.ContentJson) ? "[]" : model.ContentJson
        };

        _db.Pages.Add(page);
        await _db.SaveChangesAsync(ct);

        TempData["StatusMessage"] = $"Pagina '{page.Title}' creata.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var page = await _db.Pages.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (page is null)
        {
            return NotFound();
        }

        var model = new PageFormViewModel
        {
            Id = page.Id,
            Slug = page.Slug,
            Title = page.Title,
            ParentId = page.ParentId,
            IsPublished = page.IsPublished,
            SortOrder = page.SortOrder,
            ContentJson = page.ContentJson
        };
        await PopulateOptionsAsync(model, excludePageId: id, ct);
        return View(model);
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, PageFormViewModel model, CancellationToken ct)
    {
        model.Id = id;

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, excludePageId: id, ct);
            return View(model);
        }

        var page = await _db.Pages.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (page is null)
        {
            return NotFound();
        }

        if (await _db.Pages.AnyAsync(p => p.Slug == model.Slug && p.Id != id, ct))
        {
            ModelState.AddModelError(nameof(model.Slug), "Esiste già una pagina con questo slug.");
            await PopulateOptionsAsync(model, excludePageId: id, ct);
            return View(model);
        }

        var conflictingMenuPath = await FindConflictingInternalMenuPathAsync(model.Slug, ct);
        if (conflictingMenuPath is not null)
        {
            ModelState.AddModelError(nameof(model.Slug),
                $"Il percorso '{conflictingMenuPath}' è già usato da una voce di menu di tipo 'URL esterno'. Cambia slug o aggiorna quella voce di menu.");
            await PopulateOptionsAsync(model, excludePageId: id, ct);
            return View(model);
        }

        if (model.ParentId == id)
        {
            ModelState.AddModelError(nameof(model.ParentId), "Una pagina non può essere genitrice di se stessa.");
            await PopulateOptionsAsync(model, excludePageId: id, ct);
            return View(model);
        }

        if (model.ParentId.HasValue && await CreatesHierarchyCycleAsync(id, model.ParentId.Value, ct))
        {
            ModelState.AddModelError(nameof(model.ParentId),
                "Questa gerarchia genitore/figlio creerebbe un ciclo (una pagina non può discendere da una propria sotto-pagina).");
            await PopulateOptionsAsync(model, excludePageId: id, ct);
            return View(model);
        }

        page.Slug = model.Slug;
        page.Title = model.Title;
        page.ParentId = model.ParentId;
        page.IsPublished = model.IsPublished;
        page.SortOrder = model.SortOrder;
        page.ContentJson = string.IsNullOrWhiteSpace(model.ContentJson) ? "[]" : model.ContentJson;
        page.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        TempData["StatusMessage"] = $"Pagina '{page.Title}' aggiornata.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var page = await _db.Pages.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (page is null)
        {
            return NotFound();
        }

        if (await _db.Pages.AnyAsync(p => p.ParentId == id, ct))
        {
            TempData["ErrorMessage"] = "Non puoi eliminare una pagina che ha sotto-pagine: spostale o eliminale prima.";
            return RedirectToAction(nameof(Index));
        }

        _db.Pages.Remove(page);
        await _db.SaveChangesAsync(ct);

        TempData["StatusMessage"] = $"Pagina '{page.Title}' eliminata.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateOptionsAsync(PageFormViewModel model, Guid? excludePageId, CancellationToken ct)
    {
        model.ParentOptions = await _db.Pages
            .Where(p => excludePageId == null || p.Id != excludePageId)
            .OrderBy(p => p.Title)
            .Select(p => new PageParentOption(p.Id, p.Title))
            .ToListAsync(ct);

        model.AvailableEntities = await _db.EntityDefinitions
            .Where(e => e.IsEnabled)
            .OrderBy(e => e.DisplayName)
            .Select(e => new PageEntityOption(e.SchemaName + "." + e.TableName, e.DisplayName))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Controlla se lo slug di una pagina, tradotto in percorso interno
    /// (es. "chi-siamo" → "/chi-siamo"), collide con una voce di menu di tipo
    /// ExternalUrl già configurata con lo stesso percorso relativo. I link
    /// davvero esterni (http/https/ecc.) non partecipano a questo controllo:
    /// vedi <see cref="InternalUrlPath"/>.
    /// </summary>
    private async Task<string?> FindConflictingInternalMenuPathAsync(string slug, CancellationToken ct)
    {
        var targetPath = InternalUrlPath.FromPageSlug(slug);

        var externalUrls = await _db.MenuItems
            .Where(i => i.TargetType == MenuTargetType.ExternalUrl)
            .Select(i => i.TargetValue)
            .ToListAsync(ct);

        var hasConflict = externalUrls
            .Where(InternalUrlPath.IsInternal)
            .Select(InternalUrlPath.Normalize)
            .Any(p => p == targetPath);

        return hasConflict ? targetPath : null;
    }

    /// <summary>
    /// True se impostare <paramref name="candidateParentId"/> come genitore
    /// della pagina <paramref name="pageId"/> creerebbe un ciclo, cioè se
    /// <paramref name="candidateParentId"/> è (direttamente o indirettamente)
    /// una discendente di <paramref name="pageId"/>. Risalendo dal genitore
    /// candidato verso la radice, se si incontra di nuovo <paramref name="pageId"/>
    /// significa che si stava già scendendo nel suo stesso sottoalbero.
    /// </summary>
    private async Task<bool> CreatesHierarchyCycleAsync(Guid pageId, Guid candidateParentId, CancellationToken ct)
    {
        var parentById = await _db.Pages
            .Select(p => new { p.Id, p.ParentId })
            .ToDictionaryAsync(p => p.Id, p => p.ParentId, ct);

        Guid? current = candidateParentId;
        var visited = new HashSet<Guid>();

        while (current.HasValue)
        {
            if (current.Value == pageId)
            {
                return true;
            }

            if (!visited.Add(current.Value))
            {
                // Ciclo pre-esistente incontrato risalendo: non è colpa di
                // questa modifica, ci si ferma per evitare un loop infinito.
                return false;
            }

            current = parentById.GetValueOrDefault(current.Value);
        }

        return false;
    }
}