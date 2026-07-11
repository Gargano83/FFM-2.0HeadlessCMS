using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DAMIHeadlessCMS.Admin.ViewModels;
using DAMIHeadlessCMS.Core.Entities;
using DAMIHeadlessCMS.Data;
using DAMIHeadlessCMS.Data.Identity;

namespace DAMIHeadlessCMS.Admin.Controllers;

/// <summary>
/// Configurazione delle sorgenti di localizzazione "a chiave condivisa" (pattern
/// legacy tipo WN_LOCALIZZAZIONE/WN_LINGUE): qui si descrive la forma della
/// tabella di traduzione, poi dal wizard di scaffolding si associa la sorgente
/// ai singoli campi interi che la usano. Elenco e dettaglio sono accessibili in
/// sola lettura anche a CmsOperator; creare, modificare o eliminare una
/// sorgente resta riservato a CmsAdmin (vedi gli attributi [Authorize]
/// espliciti sulle singole azioni di scrittura).
/// </summary>
[Route("dami/localization-sources")]
[Authorize(Policy = CmsAuthConstants.LocalizationViewPolicy)]
public class LocalizationSourcesController : Controller
{
    private readonly CmsDbContext _db;

    public LocalizationSourcesController(CmsDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var sources = await _db.LocalizationSources.OrderBy(s => s.DisplayName).ToListAsync(ct);

        var usageCounts = await _db.FieldDefinitions
            .Where(f => f.LocalizationSourceId != null)
            .GroupBy(f => f.LocalizationSourceId!.Value)
            .Select(g => new { SourceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SourceId, x => x.Count, ct);

        var items = sources.Select(s => new LocalizationSourceListItemViewModel
        {
            Id = s.Id,
            DisplayName = s.DisplayName,
            ContentTable = $"{s.ContentSchemaName}.{s.ContentTableName}",
            LanguageTable = $"{s.LanguageSchemaName}.{s.LanguageTableName}",
            DefaultLanguageId = s.DefaultLanguageId,
            UsedByFieldsCount = usageCounts.GetValueOrDefault(s.Id)
        }).ToList();

        return View(items);
    }

    [HttpGet("create")]
    [Authorize(Policy = CmsAuthConstants.AdminPolicy)]
    public IActionResult Create() => View(new LocalizationSourceFormViewModel());

    [HttpPost("create")]
    [Authorize(Policy = CmsAuthConstants.AdminPolicy)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LocalizationSourceFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _db.LocalizationSources.Add(new LocalizationSource
        {
            Id = Guid.NewGuid(),
            DisplayName = model.DisplayName,
            ContentSchemaName = model.ContentSchemaName,
            ContentTableName = model.ContentTableName,
            ContentIdColumn = model.ContentIdColumn,
            LanguageIdColumn = model.LanguageIdColumn,
            TextColumn = model.TextColumn,
            RowIdColumn = model.RowIdColumn,
            LanguageSchemaName = model.LanguageSchemaName,
            LanguageTableName = model.LanguageTableName,
            LanguageIdColumnInLanguageTable = model.LanguageIdColumnInLanguageTable,
            LanguageCodeColumn = model.LanguageCodeColumn,
            LanguageNameColumn = model.LanguageNameColumn,
            DefaultLanguageId = model.DefaultLanguageId
        });

        await _db.SaveChangesAsync(ct);
        TempData["StatusMessage"] = $"Sorgente di localizzazione '{model.DisplayName}' creata.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var source = await _db.LocalizationSources.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (source is null)
        {
            return NotFound();
        }

        return View(new LocalizationSourceFormViewModel
        {
            Id = source.Id,
            DisplayName = source.DisplayName,
            ContentSchemaName = source.ContentSchemaName,
            ContentTableName = source.ContentTableName,
            ContentIdColumn = source.ContentIdColumn,
            LanguageIdColumn = source.LanguageIdColumn,
            TextColumn = source.TextColumn,
            RowIdColumn = source.RowIdColumn,
            LanguageSchemaName = source.LanguageSchemaName,
            LanguageTableName = source.LanguageTableName,
            LanguageIdColumnInLanguageTable = source.LanguageIdColumnInLanguageTable,
            LanguageCodeColumn = source.LanguageCodeColumn,
            LanguageNameColumn = source.LanguageNameColumn,
            DefaultLanguageId = source.DefaultLanguageId
        });
    }

    [HttpPost("{id:guid}/edit")]
    [Authorize(Policy = CmsAuthConstants.AdminPolicy)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, LocalizationSourceFormViewModel model, CancellationToken ct)
    {
        model.Id = id;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var source = await _db.LocalizationSources.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (source is null)
        {
            return NotFound();
        }

        source.DisplayName = model.DisplayName;
        source.ContentSchemaName = model.ContentSchemaName;
        source.ContentTableName = model.ContentTableName;
        source.ContentIdColumn = model.ContentIdColumn;
        source.LanguageIdColumn = model.LanguageIdColumn;
        source.TextColumn = model.TextColumn;
        source.RowIdColumn = model.RowIdColumn;
        source.LanguageSchemaName = model.LanguageSchemaName;
        source.LanguageTableName = model.LanguageTableName;
        source.LanguageIdColumnInLanguageTable = model.LanguageIdColumnInLanguageTable;
        source.LanguageCodeColumn = model.LanguageCodeColumn;
        source.LanguageNameColumn = model.LanguageNameColumn;
        source.DefaultLanguageId = model.DefaultLanguageId;

        await _db.SaveChangesAsync(ct);
        TempData["StatusMessage"] = $"Sorgente di localizzazione '{model.DisplayName}' aggiornata.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/delete")]
    [Authorize(Policy = CmsAuthConstants.AdminPolicy)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var source = await _db.LocalizationSources.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (source is null)
        {
            return NotFound();
        }

        if (await _db.FieldDefinitions.AnyAsync(f => f.LocalizationSourceId == id, ct))
        {
            TempData["ErrorMessage"] = "Non puoi eliminare questa sorgente: è ancora usata da alcuni campi. Rimuovi prima l'associazione dal wizard di scaffolding.";
            return RedirectToAction(nameof(Index));
        }

        _db.LocalizationSources.Remove(source);
        await _db.SaveChangesAsync(ct);
        TempData["StatusMessage"] = $"Sorgente di localizzazione '{source.DisplayName}' eliminata.";
        return RedirectToAction(nameof(Index));
    }
}