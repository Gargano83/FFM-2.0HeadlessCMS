using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MyCms.Core.Entities;
using MyCms.Data;
using MyCms.Scaffolding.Models;

namespace MyCms.Scaffolding;

/// <summary>
/// Orchestra lo scaffolding: espone le tabelle disponibili (step 1 del wizard)
/// e, dato un elenco di tabelle selezionate, popola/aggiorna EntityDefinition
/// e FieldDefinition nel CmsDbContext (step finale del wizard).
///
/// Idempotente: rilanciare lo scaffolding su una tabella già presente aggiorna
/// i metadati strutturali (colonne, tipi, FK) preservando le personalizzazioni
/// dell'utente (DisplayName, EditorType, ShowInList/Form, SortOrder) se già
/// impostate in precedenza.
/// </summary>
public class ScaffoldingService
{
    private readonly ISqlServerSchemaReader _reader;
    private readonly CmsDbContext _db;

    public ScaffoldingService(ISqlServerSchemaReader reader, CmsDbContext db)
    {
        _reader = reader;
        _db = db;
    }

    /// <summary>Tabelle disponibili nel database, da mostrare come checkbox nel wizard.</summary>
    public Task<IReadOnlyList<DatabaseTableInfo>> GetAvailableTablesAsync(CancellationToken ct = default)
        => _reader.GetTablesAsync(ct);

    /// <summary>
    /// Legge lo schema delle tabelle selezionate e salva/aggiorna i metadati
    /// corrispondenti. Le foreign key vengono collegate correttamente solo se
    /// anche la tabella referenziata fa parte della selezione (o è già stata
    /// scaffoldata in precedenza); altrimenti il campo resta una Select senza
    /// target risolto, correggibile a mano nel backoffice.
    /// </summary>
    public async Task<IReadOnlyList<EntityDefinition>> ScaffoldTablesAsync(
        IEnumerable<DatabaseTableInfo> selectedTables, CancellationToken ct = default)
    {
        var tables = selectedTables.ToList();

        // 1. Leggo lo schema completo (colonne + FK) di ogni tabella selezionata.
        var detailsByQualifiedName = new Dictionary<string, DatabaseTableDetails>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in tables)
        {
            var details = await _reader.GetTableDetailsAsync(table.SchemaName, table.TableName, ct);
            detailsByQualifiedName[table.QualifiedName] = details;
        }

        // 2. Upsert delle EntityDefinition (prima passata, senza campi) per
        //    avere già tutti gli Id disponibili quando risolviamo le FK.
        var entityByQualifiedName = new Dictionary<string, EntityDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables)
        {
            var existing = await _db.EntityDefinitions
                .Include(e => e.Fields)
                .FirstOrDefaultAsync(e => e.SchemaName == table.SchemaName && e.TableName == table.TableName, ct);

            var details = detailsByQualifiedName[table.QualifiedName];
            var primaryKeyColumn = details.Columns.FirstOrDefault(c => c.IsPrimaryKey)?.ColumnName
                ?? details.Columns.First().ColumnName;

            var entity = existing ?? new EntityDefinition { Id = Guid.NewGuid() };
            entity.TableName = table.TableName;
            entity.SchemaName = table.SchemaName;
            entity.DisplayName = existing?.DisplayName ?? Humanize(table.TableName);
            entity.PrimaryKeyColumn = primaryKeyColumn;
            entity.IsEnabled = existing?.IsEnabled ?? true;

            if (existing is null)
            {
                _db.EntityDefinitions.Add(entity);
            }

            entityByQualifiedName[table.QualifiedName] = entity;
        }

        // Anche le entità già scaffoldate in precedenza (non nella selezione
        // corrente) servono per risolvere le FK verso tabelle non riselezionate.
        var alreadyKnown = await _db.EntityDefinitions
            .Where(e => !entityByQualifiedName.Keys.Contains(e.SchemaName + "." + e.TableName))
            .ToListAsync(ct);
        foreach (var known in alreadyKnown)
        {
            entityByQualifiedName[$"{known.SchemaName}.{known.TableName}"] = known;
        }

        await _db.SaveChangesAsync(ct);

        // 3. Upsert dei FieldDefinition, risolvendo le FK verso entityByQualifiedName.
        foreach (var table in tables)
        {
            var entity = entityByQualifiedName[table.QualifiedName];
            var details = detailsByQualifiedName[table.QualifiedName];

            foreach (var column in details.Columns)
            {
                var fk = details.ForeignKeys.FirstOrDefault(f =>
                    string.Equals(f.ColumnName, column.ColumnName, StringComparison.OrdinalIgnoreCase));
                var isForeignKey = fk is not null;

                EntityDefinition? targetEntity = null;
                string? displayColumn = null;
                if (fk is not null)
                {
                    var targetQualifiedName = $"{fk.ReferencedSchema}.{fk.ReferencedTable}";
                    entityByQualifiedName.TryGetValue(targetQualifiedName, out targetEntity);

                    if (detailsByQualifiedName.TryGetValue(targetQualifiedName, out var targetDetails))
                    {
                        displayColumn = targetDetails.Columns
                            .FirstOrDefault(c => c.SqlDataType is "nvarchar" or "varchar" && !c.IsPrimaryKey)
                            ?.ColumnName
                            ?? targetDetails.Columns.FirstOrDefault(c => c.IsPrimaryKey)?.ColumnName;
                    }
                }

                var field = await _db.FieldDefinitions
                    .FirstOrDefaultAsync(f => f.EntityDefinitionId == entity.Id && f.ColumnName == column.ColumnName, ct);

                bool isNew = field is null;
                if (isNew)
                {
                    field = new FieldDefinition
                    {
                        Id = Guid.NewGuid(),
                        EntityDefinitionId = entity.Id,
                        ColumnName = column.ColumnName
                    };
                }

                field.DisplayName = isNew ? Humanize(column.ColumnName) : field.DisplayName;
                field.SqlDataType = column.SqlDataType;
                field.MaxLength = column.MaxLength;
                field.IsNullable = column.IsNullable;
                field.IsPrimaryKey = column.IsPrimaryKey;
                field.IsForeignKey = isForeignKey;
                field.ForeignKeyTargetEntityId = targetEntity?.Id;
                field.ForeignKeyDisplayColumn = displayColumn;
                field.EditorType = isNew
                    ? EditorTypeInferrer.Infer(column.SqlDataType, isForeignKey, column.MaxLength)
                    : field.EditorType;
                // Default ragionevoli: la PK e le colonne identity non si mostrano
                // nel form di creazione (sono generate dal DB), ma la PK resta
                // visibile in lista per identificare la riga.
                field.ShowInList = isNew ? true : field.ShowInList;
                field.ShowInForm = isNew ? !(column.IsPrimaryKey && column.IsIdentity) : field.ShowInForm;
                field.IsRequired = isNew
                    ? (!column.IsNullable && !column.IsIdentity && !column.IsPrimaryKey)
                    : field.IsRequired;
                field.SortOrder = isNew ? column.ColumnId : field.SortOrder;

                if (isNew)
                {
                    _db.FieldDefinitions.Add(field);
                }

                await _db.SaveChangesAsync(ct);
            }
        }

        return tables.Select(t => entityByQualifiedName[t.QualifiedName]).ToList();
    }

    /// <summary>"OrderDate" -> "Order Date", "order_date" -> "Order date".</summary>
    private static string Humanize(string identifier)
    {
        var spaced = Regex.Replace(identifier.Replace('_', ' '), "(?<=[a-z0-9])(?=[A-Z])", " ");
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced.ToLowerInvariant());
    }
}
