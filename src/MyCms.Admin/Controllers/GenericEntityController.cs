using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyCms.Admin.Data;
using MyCms.Admin.ViewModels;
using MyCms.Core.Entities;
using MyCms.Core.Enums;
using MyCms.Data;
using MyCms.Data.Identity;
using MyCms.Scaffolding;
using MyCms.Scaffolding.Models;
using System.Globalization;

namespace MyCms.Admin.Controllers;

/// <summary>
/// Controller generico per il CRUD di qualsiasi entità scaffoldata.
/// Instradato per EntityDefinition.Id (Guid) e non per nome tabella: nomi
/// tabella possono ripetersi tra schemi diversi (es. "Lega" sia in FFM che
/// in TEST), quindi il nome non è un identificatore univoco affidabile
/// per il routing.
/// </summary>
[Route("backoffice/admin")]
[Authorize(Policy = CmsAuthConstants.EditorPolicy)]
public class GenericEntityController : Controller
{
    private const int DefaultPageSize = 20;
    private const long MaxUploadBytes = 50_000_000; // 50MB, personalizzabile in futuro via options

    private readonly CmsDbContext _db;
    private readonly IGenericEntityRepository _repository;
    private readonly ScaffoldingService _scaffoldingService;

    public GenericEntityController(CmsDbContext db, IGenericEntityRepository repository, ScaffoldingService scaffoldingService)
    {
        _db = db;
        _repository = repository;
        _scaffoldingService = scaffoldingService;
    }

    /// <summary>Elenco delle entità disponibili nel backoffice (menu principale CRUD generico).</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var entities = await _db.EntityDefinitions
            .Where(e => e.IsEnabled)
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.DisplayName)
            .ToListAsync(ct);

        return View(entities);
    }

    [HttpGet("{entityId:guid}")]
    public async Task<IActionResult> List(Guid entityId, int page = 1, CancellationToken ct = default)
    {
        var entity = await LoadEntityAsync(entityId, ct);
        if (entity is null)
        {
            return NotFound();
        }

        var result = await _repository.GetListAsync(entity, page, DefaultPageSize, ct);

        return View(new GenericEntityIndexViewModel
        {
            Entity = entity,
            Rows = result.Rows,
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    [HttpGet("{entityId:guid}/create")]
    public async Task<IActionResult> Create(Guid entityId, CancellationToken ct)
    {
        var entity = await LoadEntityAsync(entityId, ct);
        if (entity is null)
        {
            return NotFound();
        }

        return View(new GenericEntityFormViewModel { Entity = entity, Values = null, RecordId = null });
    }

    [HttpPost("{entityId:guid}/create")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> CreatePost(Guid entityId, CancellationToken ct)
    {
        var entity = await LoadEntityAsync(entityId, ct);
        if (entity is null)
        {
            return NotFound();
        }

        var formValues = ReadFormValues(entity);
        var files = ReadFormFiles(entity);

        try
        {
            await _repository.CreateAsync(entity, formValues, files, ct);
            return RedirectToAction(nameof(List), new { entityId });
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or InvalidOperationException)
        {
            return View(new GenericEntityFormViewModel
            {
                Entity = entity,
                Values = formValues.ToDictionary(kv => kv.Key, kv => (object?)kv.Value),
                RecordId = null,
                ErrorMessage = ex.Message
            });
        }
    }

    [HttpGet("{entityId:guid}/edit/{recordId}")]
    public async Task<IActionResult> Edit(Guid entityId, string recordId, CancellationToken ct)
    {
        var entity = await LoadEntityAsync(entityId, ct);
        if (entity is null)
        {
            return NotFound();
        }

        var pkValue = ParsePrimaryKeyValue(entity, recordId);
        var values = await _repository.GetByIdAsync(entity, pkValue, ct);
        if (values is null)
        {
            return NotFound();
        }

        return View(new GenericEntityFormViewModel { Entity = entity, Values = values, RecordId = recordId });
    }

    [HttpPost("{entityId:guid}/edit/{recordId}")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> EditPost(Guid entityId, string recordId, CancellationToken ct)
    {
        var entity = await LoadEntityAsync(entityId, ct);
        if (entity is null)
        {
            return NotFound();
        }

        var formValues = ReadFormValues(entity);
        var files = ReadFormFiles(entity);

        try
        {
            var pkValue = ParsePrimaryKeyValue(entity, recordId);
            await _repository.UpdateAsync(entity, pkValue, formValues, files, ct);
            return RedirectToAction(nameof(List), new { entityId });
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or InvalidOperationException)
        {
            return View(new GenericEntityFormViewModel
            {
                Entity = entity,
                Values = formValues.ToDictionary(kv => kv.Key, kv => (object?)kv.Value),
                RecordId = recordId,
                ErrorMessage = ex.Message
            });
        }
    }

    [HttpPost("{entityId:guid}/delete/{recordId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid entityId, string recordId, CancellationToken ct)
    {
        var entity = await LoadEntityAsync(entityId, ct);
        if (entity is null)
        {
            return NotFound();
        }

        var pkValue = ParsePrimaryKeyValue(entity, recordId);
        await _repository.DeleteAsync(entity, pkValue, ct);

        return RedirectToAction(nameof(List), new { entityId });
    }

    /// <summary>Vista di sola lettura della struttura fisica di un'entità già scaffoldata.</summary>
    [HttpGet("{entityId:guid}/structure")]
    [Authorize(Policy = CmsAuthConstants.AdminPolicy)]
    public async Task<IActionResult> Structure(Guid entityId, CancellationToken ct)
    {
        var entity = await LoadEntityAsync(entityId, ct);
        if (entity is null)
        {
            return NotFound();
        }

        return View(entity);
    }

    [HttpPost("{entityId:guid}/structure/refresh")]
    [Authorize(Policy = CmsAuthConstants.AdminPolicy)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshStructure(Guid entityId, CancellationToken ct)
    {
        var entity = await LoadEntityAsync(entityId, ct);
        if (entity is null)
        {
            return NotFound();
        }

        await _scaffoldingService.ScaffoldTablesAsync(
            new[] { new DatabaseTableInfo(entity.SchemaName, entity.TableName) }, ct);

        TempData["StatusMessage"] = $"Struttura di '{entity.DisplayName}' aggiornata dal database.";
        return RedirectToAction(nameof(Structure), new { entityId });
    }

    /// <summary>Autocomplete: suggerimenti filtrati da 'q' per una FK.</summary>
    [HttpGet("lookup/{fieldId:guid}")]
    public async Task<IActionResult> Lookup(Guid fieldId, [FromQuery] string? q, CancellationToken ct)
    {
        var field = await _db.FieldDefinitions
            .Include(f => f.ForeignKeyTargetEntity)
                .ThenInclude(e => e!.Fields)
            .FirstOrDefaultAsync(f => f.Id == fieldId, ct);

        if (field is null || !field.IsForeignKey || field.ForeignKeyTargetEntity is null)
        {
            return NotFound();
        }

        var options = await _repository.GetLookupOptionsAsync(
            field.ForeignKeyTargetEntity, field.ForeignKeyDisplayColumn, q, ct);

        return Json(options);
    }

    /// <summary>Etichetta del valore FK già selezionato, per pre-popolare l'autocomplete in Edit.</summary>
    [HttpGet("lookup/{fieldId:guid}/label")]
    public async Task<IActionResult> LookupLabel(Guid fieldId, [FromQuery] string id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var field = await _db.FieldDefinitions
            .Include(f => f.ForeignKeyTargetEntity)
            .FirstOrDefaultAsync(f => f.Id == fieldId, ct);

        if (field is null || !field.IsForeignKey || field.ForeignKeyTargetEntity is null)
        {
            return NotFound();
        }

        var label = await _repository.GetLookupLabelAsync(field.ForeignKeyTargetEntity, field.ForeignKeyDisplayColumn, id, ct);
        return Json(new { label = label ?? id });
    }

    // --- Helpers -----------------------------------------------------

    private Task<EntityDefinition?> LoadEntityAsync(Guid entityId, CancellationToken ct)
        => _db.EntityDefinitions
            .Include(e => e.Fields)
                .ThenInclude(f => f.ForeignKeyTargetEntity)
            .FirstOrDefaultAsync(e => e.Id == entityId && e.IsEnabled, ct);

    /// <summary>Legge dal form solo le colonne marcate ShowInForm (whitelist), come si aspetta il repository.</summary>
    private Dictionary<string, string?> ReadFormValues(EntityDefinition entity)
        => entity.Fields
            .Where(f => f.ShowInForm)
            .ToDictionary(f => f.ColumnName, f => (string?)Request.Form[f.ColumnName].ToString());

    /// <summary>Estrae i file caricati per i soli campi EditorType.File.</summary>
    private Dictionary<string, IFormFile?> ReadFormFiles(EntityDefinition entity)
        => entity.Fields
            .Where(f => f.ShowInForm && f.EditorType == EditorType.File)
            .ToDictionary(f => f.ColumnName, f => Request.Form.Files.GetFile(f.ColumnName));

    /// <summary>Converte il segmento di route recordId nel tipo .NET corretto per la PK di questa entità.</summary>
    private static object ParsePrimaryKeyValue(EntityDefinition entity, string recordId)
    {
        var pkField = entity.Fields.FirstOrDefault(f => f.IsPrimaryKey)
            ?? throw new InvalidOperationException(
                $"EntityDefinition '{entity.QualifiedTableName}' non ha nessun campo marcato IsPrimaryKey.");

        return pkField.SqlDataType.ToLowerInvariant() switch
        {
            "int" => int.Parse(recordId, CultureInfo.InvariantCulture),
            "bigint" => long.Parse(recordId, CultureInfo.InvariantCulture),
            "smallint" => short.Parse(recordId, CultureInfo.InvariantCulture),
            "tinyint" => byte.Parse(recordId, CultureInfo.InvariantCulture),
            "uniqueidentifier" => Guid.Parse(recordId),
            _ => recordId
        };
    }
}