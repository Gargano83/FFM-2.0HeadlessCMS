using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyCms.Admin.ViewModels;
using MyCms.Core.Entities;
using MyCms.Data;
using MyCms.Data.Identity;

namespace MyCms.Admin.Controllers;

/// <summary>
/// CRUD delle pagine custom del CMS (contenuto a blocchi in CmsPage.ContentJson).
/// Il rendering front-end dei blocchi è responsabilità dell'app host: qui si
/// gestisce solo la composizione/editing dei blocchi in backoffice.
/// </summary>
[Route("backoffice/admin/pages")]
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

        if (model.ParentId == id)
        {
            ModelState.AddModelError(nameof(model.ParentId), "Una pagina non può essere genitrice di se stessa.");
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
}