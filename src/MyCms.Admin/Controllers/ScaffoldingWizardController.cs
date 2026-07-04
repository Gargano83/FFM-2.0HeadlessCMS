using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyCms.Admin.ViewModels;
using MyCms.Data;
using MyCms.Data.Identity;
using MyCms.Scaffolding;
using MyCms.Scaffolding.Models;

namespace MyCms.Admin.Controllers;

/// <summary>
/// Wizard di scaffolding: selezione tabelle + configurazione dei metadati
/// di presentazione (DisplayName/EditorType/ShowInList/ShowInForm), tutto
/// in un'unica sessione utente prima del salvataggio definitivo. Non altera
/// mai la struttura reale del database: legge da sys.* e scrive solo in
/// cms.EntityDefinition/cms.FieldDefinition.
/// </summary>
[Route("dami/scaffolding")]
[Authorize(Policy = CmsAuthConstants.AdminPolicy)]
public class ScaffoldingWizardController : Controller
{
    private readonly ScaffoldingService _scaffoldingService;
    private readonly CmsDbContext _db;

    public ScaffoldingWizardController(ScaffoldingService scaffoldingService, CmsDbContext db)
    {
        _scaffoldingService = scaffoldingService;
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var availableTables = await _scaffoldingService.GetAvailableTablesAsync(ct);

        var scaffoldedKeys = await _db.EntityDefinitions
            .Select(e => e.SchemaName + "." + e.TableName)
            .ToListAsync(ct);
        var scaffoldedSet = scaffoldedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var items = availableTables
            .Select(t => new ScaffoldingTableItem(t.SchemaName, t.TableName, scaffoldedSet.Contains(t.QualifiedName)))
            .OrderBy(t => t.SchemaName)
            .ThenBy(t => t.TableName)
            .ToList();

        return View(new ScaffoldingTableListViewModel { Tables = items });
    }

    /// <summary>Anteprima AJAX: struttura + eventuali personalizzazioni già salvate, senza scrivere nulla.</summary>
    [HttpGet("preview")]
    public async Task<IActionResult> Preview([FromQuery] string tables, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tables))
        {
            return BadRequest("Nessuna tabella selezionata.");
        }

        var selected = tables.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Split('.', 2))
            .Where(parts => parts.Length == 2)
            .Select(parts => new DatabaseTableInfo(parts[0], parts[1]))
            .ToList();

        if (selected.Count == 0)
        {
            return BadRequest("Formato tabelle non valido (atteso schema.tabella).");
        }

        var preview = await _scaffoldingService.PreviewAsync(selected, ct);
        return Json(preview);
    }

    /// <summary>
    /// Salvataggio unico: esegue lo scaffold strutturale idempotente e applica
    /// nello stesso passaggio le personalizzazioni scelte nel wizard.
    /// </summary>
    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] ScaffoldingSaveRequest request, CancellationToken ct)
    {
        if (request.Entities.Count == 0)
        {
            return BadRequest("Nessuna configurazione da salvare.");
        }

        var selectedTables = request.Entities
            .Select(e => new DatabaseTableInfo(e.SchemaName, e.TableName))
            .ToList();

        var entities = await _scaffoldingService.ScaffoldTablesAsync(selectedTables, ct);

        foreach (var entityRequest in request.Entities)
        {
            var entity = entities.FirstOrDefault(e =>
                string.Equals(e.SchemaName, entityRequest.SchemaName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.TableName, entityRequest.TableName, StringComparison.OrdinalIgnoreCase));

            if (entity is null)
            {
                continue;
            }

            entity.DisplayName = entityRequest.DisplayName;
            entity.Icon = entityRequest.Icon;

            foreach (var fieldRequest in entityRequest.Fields)
            {
                var field = entity.Fields.FirstOrDefault(f =>
                    string.Equals(f.ColumnName, fieldRequest.ColumnName, StringComparison.OrdinalIgnoreCase));

                if (field is null)
                {
                    continue;
                }

                field.DisplayName = fieldRequest.DisplayName;
                field.EditorType = fieldRequest.EditorType;
                field.ShowInList = fieldRequest.ShowInList;
                field.ShowInForm = fieldRequest.ShowInForm;
                field.IsRequired = fieldRequest.IsRequired;
            }
        }

        await _db.SaveChangesAsync(ct);

        return Json(new { success = true, redirectUrl = "/dami" });
    }
}