using System.Text.Json;
using DAMIHeadlessCMS.Data;
using DAMIHeadlessCMS.TestHost.Models.PublicSite;
using DAMIHeadlessCMS.TestHost.PublicSite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DAMIHeadlessCMS.TestHost.Controllers;

/// <summary>
/// Rendering pubblico di una <see cref="Core.Entities.CmsPage"/> nativa, raggiunta
/// tramite la rotta convenzionale "{slug}" (vedi Program.cs). Riservato a contenuti
/// creati direttamente da backoffice (non provenienti dal legacy, che invece vengono
/// letti dalle tabelle scaffoldate — vedi HomeController/LegacyContentReader).
/// Interpreta i blocchi "html" (testo/immagini) ed "entityList" (contenuto da
/// un'entità scaffoldata, risolto qui prima di arrivare alla view — vedi
/// <see cref="ResolveBlocksAsync"/>). Il tipo "component" (per i componenti Angular
/// del modulo FFM) resta non gestito qui: verrà cablato quando servirà davvero un
/// caso d'uso che lo richieda su una pagina CMS nativa.
/// </summary>
public class PagesController : Controller
{
    private readonly CmsDbContext _db;
    private readonly LegacyContentReader _content;

    public PagesController(CmsDbContext db, LegacyContentReader content)
    {
        _db = db;
        _content = content;
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

        var blocks = await ResolveBlocksAsync(page.ContentJson, ct);
        return View(new CmsPageViewModel { Title = page.Title, Blocks = blocks });
    }

    /// <summary>
    /// Interpreta CmsPage.ContentJson blocco per blocco. Un blocco "entityList" che
    /// referenzia un'entità non (più) scaffoldata viene silenziosamente omesso (la
    /// pagina resta comunque fruibile) invece di far fallire l'intera pagina: stessa
    /// filosofia già usata per un ContentJson non valido, vedi il try/catch sotto.
    /// </summary>
    private async Task<IReadOnlyList<CmsPageBlockViewModel>> ResolveBlocksAsync(string contentJson, CancellationToken ct)
    {
        var blocks = new List<CmsPageBlockViewModel>();

        foreach (var block in CmsPageContentParser.ParseBlocks(contentJson))
        {
            var type = block.TryGetProperty("type", out var t) ? t.GetString() : null;

            if (type == "html" && block.TryGetProperty("html", out var html))
            {
                blocks.Add(new CmsHtmlBlockViewModel(html.GetString() ?? string.Empty));
                continue;
            }

            if (type == "entityList" && block.TryGetProperty("entity", out var entityProp))
            {
                var resolved = await ResolveEntityListBlockAsync(block, entityProp.GetString(), ct);
                if (resolved is not null)
                {
                    blocks.Add(resolved);
                }
            }

            // Tipo "component" (o sconosciuto): non ancora gestito da questa view.
        }

        return blocks;
    }

    private async Task<CmsEntityListBlockViewModel?> ResolveEntityListBlockAsync(
        JsonElement block, string? qualifiedName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName))
        {
            return null;
        }

        var parts = qualifiedName.Split('.', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        var entity = await _content.GetEntityAsync(parts[0], parts[1], ct);
        if (entity is null)
        {
            return null;
        }

        var title = block.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
        var maxRows = block.TryGetProperty("maxRows", out var maxRowsProp) && maxRowsProp.TryGetInt32(out var parsed) && parsed > 0
            ? parsed
            : 50;

        var columns = entity.Fields
            .Where(f => f.ShowInList)
            .OrderBy(f => f.SortOrder)
            .Select(f => new CmsEntityListColumnViewModel(f.ColumnName, f.DisplayName, f.EditorType))
            .ToList();

        var rows = await _content.GetRowsForDisplayAsync(entity, maxRows, ct);

        return new CmsEntityListBlockViewModel(string.IsNullOrWhiteSpace(title) ? null : title, entity.DisplayName, columns, rows);
    }
}
