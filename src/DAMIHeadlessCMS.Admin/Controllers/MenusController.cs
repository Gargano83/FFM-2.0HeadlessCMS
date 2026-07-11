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
/// Gestione dei menu di navigazione (CmsMenu + CmsMenuItem ad albero).
/// Il salvataggio della struttura avviene in un unico passaggio "full replace":
/// il client invia l'intero albero corrente (con id client-side per i nodi
/// nuovi) e il server ricostruisce da zero le righe di cms.MenuItem per quel
/// menu, evitando una logica di diff/merge per riordini e annidamenti misti
/// nella stessa sessione di editing.
/// </summary>
[Route("dami/menus")]
[Authorize(Policy = CmsAuthConstants.EditorPolicy)]
public class MenusController : Controller
{
    private readonly CmsDbContext _db;

    public MenusController(CmsDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var menus = await _db.Menus.ToListAsync(ct);
        var counts = await _db.MenuItems
            .GroupBy(i => i.MenuId)
            .Select(g => new { MenuId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MenuId, x => x.Count, ct);

        var items = menus
            .OrderBy(m => m.Name)
            .Select(m => new MenuListItemViewModel
            {
                Id = m.Id,
                Name = m.Name,
                ItemCount = counts.GetValueOrDefault(m.Id)
            })
            .ToList();

        return View(items);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["ErrorMessage"] = "Il nome del menu è obbligatorio.";
            return RedirectToAction(nameof(Index));
        }

        if (await _db.Menus.AnyAsync(m => m.Name == name, ct))
        {
            TempData["ErrorMessage"] = $"Esiste già un menu chiamato '{name}'.";
            return RedirectToAction(nameof(Index));
        }

        var menu = new CmsMenu { Id = Guid.NewGuid(), Name = name };
        _db.Menus.Add(menu);
        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Edit), new { menuId = menu.Id });
    }

    [HttpPost("{menuId:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid menuId, CancellationToken ct)
    {
        var menu = await _db.Menus.FirstOrDefaultAsync(m => m.Id == menuId, ct);
        if (menu is null)
        {
            return NotFound();
        }

        _db.Menus.Remove(menu); // cascade su MenuItem (OnDelete Cascade in CmsMenuConfiguration)
        await _db.SaveChangesAsync(ct);

        TempData["StatusMessage"] = $"Menu '{menu.Name}' eliminato.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{menuId:guid}/edit")]
    public async Task<IActionResult> Edit(Guid menuId, CancellationToken ct)
    {
        var menu = await _db.Menus.Include(m => m.Items).FirstOrDefaultAsync(m => m.Id == menuId, ct);
        if (menu is null)
        {
            return NotFound();
        }

        var itemsForClient = menu.Items
            .OrderBy(i => i.SortOrder)
            .Select(i => new
            {
                clientId = i.Id.ToString(),
                parentClientId = i.ParentId?.ToString(),
                label = i.Label,
                targetType = (int)i.TargetType,
                targetValue = i.TargetValue,
                openInNewTab = i.OpenInNewTab,
                sortOrder = i.SortOrder
            })
            .ToList();

        var pages = await _db.Pages
            .OrderBy(p => p.Title)
            .Select(p => new MenuPageOption(p.Slug, p.Title))
            .ToListAsync(ct);

        var entities = await _db.EntityDefinitions
            .Where(e => e.IsEnabled)
            .OrderBy(e => e.DisplayName)
            .Select(e => new MenuEntityOption(e.SchemaName + "." + e.TableName, e.DisplayName))
            .ToListAsync(ct);

        return View(new MenuEditorViewModel
        {
            MenuId = menu.Id,
            MenuName = menu.Name,
            ItemsJson = System.Text.Json.JsonSerializer.Serialize(itemsForClient),
            AvailablePages = pages,
            AvailableEntities = entities
        });
    }

    [HttpPost("{menuId:guid}/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Guid menuId, [FromBody] MenuSaveRequest request, CancellationToken ct)
    {
        var menu = await _db.Menus.FirstOrDefaultAsync(m => m.Id == menuId, ct);
        if (menu is null)
        {
            return NotFound();
        }

        var validationError = await ValidateInternalUrlUniquenessAsync(menuId, request.Items, ct);
        if (validationError is not null)
        {
            return BadRequest(new { success = false, message = validationError });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var existingItems = await _db.MenuItems.Where(i => i.MenuId == menuId).ToListAsync(ct);
        _db.MenuItems.RemoveRange(existingItems);
        await _db.SaveChangesAsync(ct);

        var idMap = request.Items.ToDictionary(i => i.ClientId, _ => Guid.NewGuid());

        var newItems = request.Items.Select(item => new CmsMenuItem
        {
            Id = idMap[item.ClientId],
            MenuId = menuId,
            ParentId = item.ParentClientId is not null ? idMap.GetValueOrDefault(item.ParentClientId) : null,
            Label = item.Label,
            TargetType = item.TargetType,
            TargetValue = item.TargetValue,
            OpenInNewTab = item.OpenInNewTab,
            SortOrder = item.SortOrder
        }).ToList();

        _db.MenuItems.AddRange(newItems);
        await _db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        return Json(new { success = true });
    }

    /// <summary>
    /// Garantisce che i percorsi "interni" (<see cref="InternalUrlPath.IsInternal"/>)
    /// usati dalle voci ExternalUrl di questo salvataggio siano univoci: né
    /// duplicati tra loro, né in conflitto con voci ExternalUrl di ALTRI menu
    /// (questo menu viene comunque sovrascritto per intero da questo save), né
    /// con lo slug di una CmsPage esistente — in quel caso la voce corretta è
    /// una voce di tipo "Pagina", non "URL esterno". I link davvero esterni
    /// (http/https/mailto/ecc.) non sono toccati da questo controllo.
    /// Restituisce il messaggio d'errore da mostrare, o null se tutto ok.
    /// </summary>
    private async Task<string?> ValidateInternalUrlUniquenessAsync(Guid menuId, IReadOnlyList<MenuSaveItem> items, CancellationToken ct)
    {
        var internalTargets = items
            .Where(i => i.TargetType == MenuTargetType.ExternalUrl && InternalUrlPath.IsInternal(i.TargetValue))
            .Select(i => InternalUrlPath.Normalize(i.TargetValue))
            .ToList();

        var duplicateWithinRequest = internalTargets
            .GroupBy(p => p, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateWithinRequest is not null)
        {
            return $"Il percorso '{duplicateWithinRequest.Key}' è usato da più voci in questo menu: dev'essere univoco.";
        }

        var normalizedPaths = internalTargets.ToHashSet(StringComparer.Ordinal);
        if (normalizedPaths.Count == 0)
        {
            return null;
        }

        var otherMenuExternalUrls = await _db.MenuItems
            .Where(i => i.MenuId != menuId && i.TargetType == MenuTargetType.ExternalUrl)
            .Select(i => i.TargetValue)
            .ToListAsync(ct);

        var collidingWithOtherMenu = otherMenuExternalUrls
            .Where(InternalUrlPath.IsInternal)
            .Select(InternalUrlPath.Normalize)
            .FirstOrDefault(normalizedPaths.Contains);
        if (collidingWithOtherMenu is not null)
        {
            return $"Il percorso '{collidingWithOtherMenu}' è già usato da una voce di un altro menu.";
        }

        var pageSlugs = await _db.Pages.Select(p => p.Slug).ToListAsync(ct);
        var collidingWithPage = pageSlugs
            .Select(InternalUrlPath.FromPageSlug)
            .FirstOrDefault(normalizedPaths.Contains);
        if (collidingWithPage is not null)
        {
            return $"Il percorso '{collidingWithPage}' corrisponde già allo slug di una pagina esistente: usa una voce di tipo 'Pagina' invece di 'URL esterno'.";
        }

        return null;
    }
}