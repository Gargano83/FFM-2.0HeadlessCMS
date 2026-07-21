using DAMIHeadlessCMS.Admin.Utilities;
using DAMIHeadlessCMS.Admin.ViewModels;
using DAMIHeadlessCMS.Core.Enums;
using DAMIHeadlessCMS.Data;
using DAMIHeadlessCMS.Data.Identity;
using DAMIHeadlessCMS.Scaffolding;
using DAMIHeadlessCMS.Scaffolding.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DAMIHeadlessCMS.Admin.Controllers;

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

        var availableLocalizationSources = await _db.LocalizationSources
            .OrderBy(s => s.DisplayName)
            .Select(s => new LocalizationSourceOption(s.Id, s.DisplayName))
            .ToListAsync(ct);

        return View(new ScaffoldingTableListViewModel
        {
            Tables = items,
            AvailableLocalizationSources = availableLocalizationSources
        });
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
    /// Colonne di una tabella qualsiasi (scaffoldata o meno), per popolare le select
    /// "colonna etichetta"/"colonna filtro" quando si configura un riferimento manuale.
    /// </summary>
    [HttpGet("columns")]
    public async Task<IActionResult> Columns([FromQuery] string schema, [FromQuery] string table, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(table))
        {
            return BadRequest("Schema e tabella sono obbligatori.");
        }

        var columns = await _scaffoldingService.GetTableColumnsAsync(schema, table, ct);
        return Json(columns);
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

        // Tabelle referenziate come target di un riferimento manuale ma non incluse
        // nella selezione dell'utente: le scaffoldiamo comunque, con impostazioni di
        // default (l'utente potrà rifinirle in un secondo momento da /dami/scaffolding),
        // altrimenti non ci sarebbe nessuna EntityDefinition a cui agganciare la FK.
        var manualTargets = request.Entities
            .SelectMany(e => e.Fields)
            .Where(f => !string.IsNullOrWhiteSpace(f.ForeignKeyTargetSchema) && !string.IsNullOrWhiteSpace(f.ForeignKeyTargetTable))
            .Select(f => new DatabaseTableInfo(f.ForeignKeyTargetSchema!, f.ForeignKeyTargetTable!))
            .Distinct()
            .Where(t => !selectedTables.Any(s =>
                string.Equals(s.SchemaName, t.SchemaName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.TableName, t.TableName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var entities = await _scaffoldingService.ScaffoldTablesAsync(selectedTables.Concat(manualTargets), ct);

        var validationError = await ValidateDetailRoutePrefixesAsync(request.Entities, entities, ct);
        if (validationError is not null)
        {
            return BadRequest(new { success = false, message = validationError });
        }

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

            var normalizedPrefix = string.IsNullOrWhiteSpace(entityRequest.DetailRoutePrefix)
                ? null
                : InternalUrlPath.Normalize(entityRequest.DetailRoutePrefix);
            entity.DetailRoutePrefix = normalizedPrefix;

            // Il campo chiave ha senso solo insieme al prefisso: se il
            // prefisso non è (più) valorizzato, si azzera anche il campo,
            // anche se il client avesse comunque inviato un valore.
            entity.DetailKeyFieldId = normalizedPrefix is not null && !string.IsNullOrWhiteSpace(entityRequest.DetailKeyColumnName)
                ? entity.Fields.FirstOrDefault(f =>
                    string.Equals(f.ColumnName, entityRequest.DetailKeyColumnName, StringComparison.OrdinalIgnoreCase))?.Id
                : null;

            foreach (var fieldRequest in entityRequest.Fields)
            {
                var field = entity.Fields.FirstOrDefault(f =>
                    string.Equals(f.ColumnName, fieldRequest.ColumnName, StringComparison.OrdinalIgnoreCase));

                if (field is null)
                {
                    continue;
                }

                field.DisplayName = fieldRequest.DisplayName;
                // Difesa in profondità: un campo localizzato deve sempre avere un editor
                // testuale, indipendentemente da cosa arriva dal client (che ora lo previene
                // già in UI, ma qui garantiamo la coerenza anche in caso di richieste dirette).
                field.EditorType = fieldRequest.LocalizationSourceId.HasValue
                    && fieldRequest.EditorType is not (EditorType.Text or EditorType.TextArea or EditorType.RichText)
                        ? EditorType.Text
                        : fieldRequest.EditorType;
                field.ShowInList = fieldRequest.ShowInList;
                field.ShowInForm = fieldRequest.ShowInForm;
                field.IsRequired = fieldRequest.IsRequired;
                field.LocalizationSourceId = fieldRequest.LocalizationSourceId;

                // Riferimento manuale: applicato solo se esplicitamente specificato in
                // questa richiesta, per non toccare mai un'eventuale FK fisica già
                // rilevata automaticamente da ScaffoldTablesAsync su un campo che qui
                // non è stato configurato manualmente.
                if (!string.IsNullOrWhiteSpace(fieldRequest.ForeignKeyTargetSchema) &&
                    !string.IsNullOrWhiteSpace(fieldRequest.ForeignKeyTargetTable))
                {
                    var target = entities.FirstOrDefault(e =>
                        string.Equals(e.SchemaName, fieldRequest.ForeignKeyTargetSchema, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(e.TableName, fieldRequest.ForeignKeyTargetTable, StringComparison.OrdinalIgnoreCase));

                    if (target is not null)
                    {
                        field.IsForeignKey = true;
                        field.ForeignKeyTargetEntityId = target.Id;
                        field.ForeignKeyDisplayColumn = fieldRequest.ForeignKeyDisplayColumn;
                        field.ForeignKeyFiltersJson = IsValidFilterJson(fieldRequest.ForeignKeyFiltersJson)
                            ? fieldRequest.ForeignKeyFiltersJson
                            : null;
                    }
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        return Json(new { success = true, redirectUrl = "/dami" });
    }

    /// <summary>Verifica solo che sia un JSON sintatticamente valido: la semantica (colonne esistenti, tipi) resta a carico di chi la consuma.</summary>
    private static bool IsValidFilterJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            System.Text.Json.JsonDocument.Parse(json);
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Garantisce che i <c>DetailRoutePrefix</c> di questo salvataggio siano
    /// percorsi interni validi (vedi <see cref="InternalUrlPath"/>) e univoci
    /// nello spazio di URL che il CMS conosce: né duplicati tra le entità di
    /// questo stesso salvataggio, né in conflitto con un'ALTRA entità già
    /// configurata, né con lo slug di una CmsPage, né con un percorso interno
    /// ExternalUrl di menu. Restituisce il messaggio d'errore, o null se ok.
    /// </summary>
    private async Task<string?> ValidateDetailRoutePrefixesAsync(
        IReadOnlyList<ScaffoldingSaveEntity> requestEntities,
        IReadOnlyList<Core.Entities.EntityDefinition> savedEntities,
        CancellationToken ct)
    {
        var normalizedByEntity = new List<(ScaffoldingSaveEntity Request, string NormalizedPrefix)>();

        foreach (var entityRequest in requestEntities)
        {
            if (string.IsNullOrWhiteSpace(entityRequest.DetailRoutePrefix))
            {
                continue;
            }

            if (!InternalUrlPath.IsInternal(entityRequest.DetailRoutePrefix))
            {
                return $"Il prefisso di dettaglio '{entityRequest.DetailRoutePrefix}' di {entityRequest.SchemaName}.{entityRequest.TableName} " +
                       "dev'essere un percorso interno (es. \"/categorie\"), non un URL esterno.";
            }

            normalizedByEntity.Add((entityRequest, InternalUrlPath.Normalize(entityRequest.DetailRoutePrefix)));
        }

        if (normalizedByEntity.Count == 0)
        {
            return null;
        }

        var duplicateGroup = normalizedByEntity
            .GroupBy(x => x.NormalizedPrefix, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateGroup is not null)
        {
            return $"Il prefisso di dettaglio '{duplicateGroup.Key}' è usato da più entità in questo salvataggio: dev'essere univoco.";
        }

        // Id delle entità coinvolte in QUESTO salvataggio, da escludere dal
        // controllo contro le altre EntityDefinition già persistite (altrimenti
        // un'entità colliderebbe sempre con se stessa in un semplice re-save).
        var savedIds = requestEntities
            .Select(er => savedEntities.FirstOrDefault(e =>
                string.Equals(e.SchemaName, er.SchemaName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.TableName, er.TableName, StringComparison.OrdinalIgnoreCase))?.Id)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        var normalizedPaths = normalizedByEntity.Select(x => x.NormalizedPrefix).ToHashSet(StringComparer.Ordinal);

        var otherEntityPrefixes = await _db.EntityDefinitions
            .Where(e => e.DetailRoutePrefix != null && !savedIds.Contains(e.Id))
            .Select(e => e.DetailRoutePrefix!)
            .ToListAsync(ct);

        var collidingEntity = otherEntityPrefixes
            .Select(InternalUrlPath.Normalize)
            .FirstOrDefault(normalizedPaths.Contains);
        if (collidingEntity is not null)
        {
            return $"Il prefisso di dettaglio '{collidingEntity}' è già usato da un'altra entità.";
        }

        var pageSlugs = await _db.Pages.Select(p => p.Slug).ToListAsync(ct);
        var collidingPage = pageSlugs
            .Select(InternalUrlPath.FromPageSlug)
            .FirstOrDefault(normalizedPaths.Contains);
        if (collidingPage is not null)
        {
            return $"Il prefisso di dettaglio '{collidingPage}' corrisponde già allo slug di una pagina esistente.";
        }

        var menuExternalUrls = await _db.MenuItems
            .Where(i => i.TargetType == MenuTargetType.ExternalUrl)
            .Select(i => i.TargetValue)
            .ToListAsync(ct);
        var collidingMenu = menuExternalUrls
            .Where(InternalUrlPath.IsInternal)
            .Select(InternalUrlPath.Normalize)
            .FirstOrDefault(normalizedPaths.Contains);
        if (collidingMenu is not null)
        {
            return $"Il prefisso di dettaglio '{collidingMenu}' è già usato da una voce di menu di tipo 'URL esterno'.";
        }

        return null;
    }
}