using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using DAMIHeadlessCMS.Core.Entities;
using DAMIHeadlessCMS.Core.Enums;
using System.Data;
using System.Globalization;

namespace DAMIHeadlessCMS.Admin.Data;

public class GenericEntityRepository : IGenericEntityRepository
{
    private readonly string _connectionString;
    private readonly IFileStorageProvider _fileStorage;

    public GenericEntityRepository(string connectionString, IFileStorageProvider fileStorage)
    {
        _connectionString = connectionString;
        _fileStorage = fileStorage;
    }

    public async Task<GenericEntityPage> GetListAsync(
        EntityDefinition entity, int page, int pageSize, bool resolveForeignKeys = false, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var listFields = entity.Fields.Where(f => f.ShowInList).OrderBy(f => f.SortOrder).ToList();
        if (listFields.Count == 0)
        {
            listFields = entity.Fields.Where(f => f.IsPrimaryKey).ToList();
        }

        var pkColumn = GetPrimaryKeyField(entity);
        var qualifiedTable = QualifiedTable(entity);
        const string alias = "t";
        var selectColumns = string.Join(", ", listFields.Select(f => BuildSelectExpression(f, alias, resolveForeignKeys)));

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        int totalCount;
        await using (var countCmd = new SqlCommand($"SELECT COUNT(*) FROM {qualifiedTable} {alias};", connection))
        {
            totalCount = (int)(await countCmd.ExecuteScalarAsync(ct))!;
        }

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        var sql = $"""
            SELECT {selectColumns}
            FROM {qualifiedTable} {alias}
            ORDER BY {alias}.{QuoteIdentifier(pkColumn.ColumnName)}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        await using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add(new SqlParameter("@Offset", SqlDbType.Int) { Value = (page - 1) * pageSize });
            command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize });

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                rows.Add(ReadRow(reader, listFields.Select(f => f.ColumnName)));
            }
        }

        return new GenericEntityPage(rows, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyDictionary<string, object?>?> GetByIdAsync(
        EntityDefinition entity, object id, CancellationToken ct = default)
    {
        var formFields = entity.Fields.OrderBy(f => f.SortOrder).ToList();
        var pkField = GetPrimaryKeyField(entity);
        var qualifiedTable = QualifiedTable(entity);
        const string alias = "t";
        var selectColumns = string.Join(", ", formFields.Select(f => BuildSelectExpression(f, alias)));

        var sql = $"""
            SELECT {selectColumns}
            FROM {qualifiedTable} {alias}
            WHERE {alias}.{QuoteIdentifier(pkField.ColumnName)} = @Id;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(BuildParameter("@Id", pkField, id));

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return ReadRow(reader, formFields.Select(f => f.ColumnName));
    }

    public async Task<object> CreateAsync(
        EntityDefinition entity,
        IReadOnlyDictionary<string, string?> formValues,
        IReadOnlyDictionary<string, IFormFile?> files,
        CancellationToken ct = default)
    {
        var pkField = GetPrimaryKeyField(entity);

        var insertFields = entity.Fields.Where(f => f.ShowInForm).OrderBy(f => f.SortOrder).ToList();

        object? explicitPkValue = null;
        if (!pkField.IsIdentity && !insertFields.Contains(pkField))
        {
            if (string.Equals(pkField.SqlDataType, "uniqueidentifier", StringComparison.OrdinalIgnoreCase))
            {
                explicitPkValue = Guid.NewGuid();
                insertFields.Insert(0, pkField);
            }
            else
            {
                throw new InvalidOperationException(
                    $"La chiave primaria '{pkField.ColumnName}' di '{entity.QualifiedTableName}' non è IDENTITY " +
                    "e non è tra i campi del form: impossibile generarla automaticamente per il tipo " +
                    $"'{pkField.SqlDataType}'.");
            }
        }

        var qualifiedTable = QualifiedTable(entity);
        var columnList = string.Join(", ", insertFields.Select(f => QuoteIdentifier(f.ColumnName)));
        var paramList = string.Join(", ", insertFields.Select((f, i) => $"@p{i}"));

        var sql = $"""
            INSERT INTO {qualifiedTable} ({columnList})
            OUTPUT INSERTED.{QuoteIdentifier(pkField.ColumnName)}
            VALUES ({paramList});
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        try
        {
            await using var command = new SqlCommand(sql, connection, transaction);
            for (var i = 0; i < insertFields.Count; i++)
            {
                var field = insertFields[i];
                var value = field == pkField && explicitPkValue is not null
                    ? explicitPkValue
                    : await ResolveFieldValueAsync(connection, transaction, entity, field, formValues, files, existingContentId: null, ct);
                command.Parameters.Add(BuildParameter($"@p{i}", field, value));
            }

            var result = await command.ExecuteScalarAsync(ct)
                ?? throw new InvalidOperationException("INSERT non ha restituito la chiave primaria generata.");

            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task UpdateAsync(
        EntityDefinition entity,
        object id,
        IReadOnlyDictionary<string, string?> formValues,
        IReadOnlyDictionary<string, IFormFile?> files,
        CancellationToken ct = default)
    {
        var pkField = GetPrimaryKeyField(entity);

        // La PK non si aggiorna mai via update generico. Un campo File senza un
        // nuovo file caricato viene escluso dal SET: preserva il valore esistente.
        var updateFields = entity.Fields
            .Where(f => f.ShowInForm && !f.IsPrimaryKey)
            .Where(f => f.EditorType != EditorType.File || files.GetValueOrDefault(f.ColumnName) is { Length: > 0 })
            .OrderBy(f => f.SortOrder)
            .ToList();

        if (updateFields.Count == 0)
        {
            return;
        }

        var qualifiedTable = QualifiedTable(entity);
        var setClause = string.Join(", ", updateFields.Select((f, i) => $"{QuoteIdentifier(f.ColumnName)} = @p{i}"));

        var sql = $"""
            UPDATE {qualifiedTable}
            SET {setClause}
            WHERE {QuoteIdentifier(pkField.ColumnName)} = @Id;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        try
        {
            // Per i campi localizzati serve il CONT_ID attualmente salvato, per capire se
            // aggiornare la riga di traduzione esistente o inserirne una nuova.
            var existingContentIds = await GetExistingLocalizedValuesAsync(connection, transaction, entity, pkField, id, updateFields, ct);

            await using var command = new SqlCommand(sql, connection, transaction);
            for (var i = 0; i < updateFields.Count; i++)
            {
                var field = updateFields[i];
                var existingContentId = existingContentIds.GetValueOrDefault(field.ColumnName);
                var value = await ResolveFieldValueAsync(connection, transaction, entity, field, formValues, files, existingContentId, ct);
                command.Parameters.Add(BuildParameter($"@p{i}", field, value));
            }
            command.Parameters.Add(BuildParameter("@Id", pkField, id));

            var affected = await command.ExecuteNonQueryAsync(ct);
            if (affected == 0)
            {
                throw new InvalidOperationException(
                    $"Nessuna riga aggiornata in '{entity.QualifiedTableName}' per {pkField.ColumnName}={id}.");
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task DeleteAsync(EntityDefinition entity, object id, CancellationToken ct = default)
    {
        var pkField = GetPrimaryKeyField(entity);
        var qualifiedTable = QualifiedTable(entity);

        var sql = $"DELETE FROM {qualifiedTable} WHERE {QuoteIdentifier(pkField.ColumnName)} = @Id;";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(BuildParameter("@Id", pkField, id));

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<LookupOption>> GetLookupOptionsAsync(
        EntityDefinition targetEntity,
        string? displayColumn,
        string? searchText,
        IReadOnlyList<ForeignKeyFilterCondition>? filters = null,
        CancellationToken ct = default)
    {
        var pkField = GetPrimaryKeyField(targetEntity);
        var labelColumnName = displayColumn ?? pkField.ColumnName;
        var (fromClause, labelExpression) = BuildForeignKeyLabelSource(targetEntity, labelColumnName, "fk");
        filters ??= [];

        var whereClauses = new List<string>();
        var parameters = new List<SqlParameter>();
        for (var i = 0; i < filters.Count; i++)
        {
            var condition = filters[i];
            var field = FindFilterableField(targetEntity, condition.ColumnName);
            var paramName = $"@ff{i}";
            whereClauses.Add($"fk.{QuoteIdentifier(field.ColumnName)} {SqlOperator(condition.Operator)} {paramName}");
            parameters.Add(BuildParameter(paramName, field, ConvertFormValue(field, condition.Value)));
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            whereClauses.Add($"{labelExpression} LIKE @Search");
            parameters.Add(new SqlParameter("@Search", SqlDbType.NVarChar) { Value = $"%{searchText}%" });
        }

        var whereClause = whereClauses.Count == 0 ? "" : $"WHERE {string.Join(" AND ", whereClauses)}";

        var sql = $"""
            SELECT TOP (50) fk.{QuoteIdentifier(pkField.ColumnName)}, {labelExpression}
            FROM {fromClause}
            {whereClause}
            ORDER BY {labelExpression};
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        var results = new List<LookupOption>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var value = reader.GetValue(0);
            var label = reader.IsDBNull(1) ? value.ToString()! : reader.GetValue(1).ToString()!;
            results.Add(new LookupOption(value.ToString()!, label));
        }

        return results;
    }

    public async Task<string?> GetLookupLabelAsync(
        EntityDefinition targetEntity, string? displayColumn, object id, CancellationToken ct = default)
    {
        var pkField = GetPrimaryKeyField(targetEntity);
        var labelColumnName = displayColumn ?? pkField.ColumnName;
        var (fromClause, labelExpression) = BuildForeignKeyLabelSource(targetEntity, labelColumnName, "fk");

        var sql = $"""
            SELECT {labelExpression}
            FROM {fromClause}
            WHERE fk.{QuoteIdentifier(pkField.ColumnName)} = @Id;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(BuildParameter("@Id", pkField, ConvertIdForLookup(pkField, id)));

        var result = await command.ExecuteScalarAsync(ct);
        return result is null or DBNull ? null : result.ToString();
    }

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        EntityDefinition entity,
        IReadOnlyList<QueryFilter>? filters = null,
        IReadOnlyList<QuerySort>? sort = null,
        int top = 100,
        CancellationToken ct = default)
    {
        top = Math.Clamp(top, 1, 500);
        filters ??= [];
        sort ??= [];

        var selectFields = entity.Fields.OrderBy(f => f.SortOrder).ToList();
        var qualifiedTable = QualifiedTable(entity);
        const string alias = "t";
        var selectColumns = string.Join(", ", selectFields.Select(f => BuildSelectExpression(f, alias)));

        var whereClauses = new List<string>();
        var parameters = new List<SqlParameter>();
        for (var i = 0; i < filters.Count; i++)
        {
            var filter = filters[i];
            var field = FindFilterableField(entity, filter.ColumnName);
            var paramName = $"@f{i}";
            whereClauses.Add($"{alias}.{QuoteIdentifier(field.ColumnName)} {SqlOperator(filter.Operator)} {paramName}");
            parameters.Add(BuildParameter(paramName, field, filter.Value));
        }

        var orderByClauses = sort
            .Select(s => $"{alias}.{QuoteIdentifier(FindFilterableField(entity, s.ColumnName).ColumnName)}{(s.Descending ? " DESC" : "")}")
            .ToList();
        // Se non viene richiesto un ordinamento esplicito, ordina comunque per PK per un
        // risultato deterministico (stesso criterio "minimo" usato da GetListAsync).
        if (orderByClauses.Count == 0)
        {
            orderByClauses.Add($"{alias}.{QuoteIdentifier(GetPrimaryKeyField(entity).ColumnName)}");
        }

        var whereClause = whereClauses.Count == 0 ? "" : $"WHERE {string.Join(" AND ", whereClauses)}";

        var sql = $"""
            SELECT TOP (@Top) {selectColumns}
            FROM {qualifiedTable} {alias}
            {whereClause}
            ORDER BY {string.Join(", ", orderByClauses)};
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Top", SqlDbType.Int) { Value = top });
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(ReadRow(reader, selectFields.Select(f => f.ColumnName)));
        }

        return rows;
    }

    /// <summary>
    /// Risolve un FieldDefinition per nome colonna, valido come filtro/ordinamento di
    /// QueryAsync: deve esistere ed essere NON localizzato (vedi commento sull'interfaccia).
    /// </summary>
    private static FieldDefinition FindFilterableField(EntityDefinition entity, string columnName)
    {
        var field = entity.Fields.FirstOrDefault(f => string.Equals(f.ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"'{columnName}' non è un campo scaffoldato di '{entity.QualifiedTableName}'.");

        if (field.IsLocalized)
        {
            throw new InvalidOperationException(
                $"'{columnName}' è un campo localizzato: non può essere usato come filtro/ordinamento diretto " +
                "in QueryAsync (il valore fisico è una chiave, non il testo tradotto).");
        }

        return field;
    }

    private static string SqlOperator(QueryFilterOperator op) => op switch
    {
        QueryFilterOperator.Equal => "=",
        QueryFilterOperator.NotEqual => "<>",
        QueryFilterOperator.GreaterThan => ">",
        QueryFilterOperator.GreaterThanOrEqual => ">=",
        QueryFilterOperator.LessThan => "<",
        QueryFilterOperator.LessThanOrEqual => "<=",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
    };

    // --- Helpers -----------------------------------------------------

    private static FieldDefinition GetPrimaryKeyField(EntityDefinition entity)
        => entity.Fields.FirstOrDefault(f => f.IsPrimaryKey)
           ?? throw new InvalidOperationException(
               $"EntityDefinition '{entity.QualifiedTableName}' non ha nessun campo marcato IsPrimaryKey.");

    private static string QualifiedTable(EntityDefinition entity)
        => $"{QuoteIdentifier(entity.SchemaName)}.{QuoteIdentifier(entity.TableName)}";

    private static string QuoteIdentifier(string name) => "[" + name.Replace("]", "]]") + "]";

    private static Dictionary<string, object?> ReadRow(SqlDataReader reader, IEnumerable<string> columnNames)
    {
        var row = new Dictionary<string, object?>();
        var names = columnNames.ToList();
        for (var i = 0; i < names.Count; i++)
        {
            var value = reader.GetValue(i);
            row[names[i]] = value is DBNull ? null : value;
        }
        return row;
    }

    /// <summary>
    /// Espressione SELECT per un campo: colonna diretta con alias, oppure — se il campo
    /// è localizzato — una subquery correlata che risolve il testo tradotto dalla
    /// LocalizationSource configurata, filtrando per la lingua di default (nessun
    /// selettore multi-lingua per ora). Il DefaultLanguageId è un metadato configurato
    /// solo da CmsAdmin, non input utente a runtime: incorporarlo nel testo SQL è sicuro.
    /// </summary>
    /// <param name="resolveForeignKeys">
    /// True solo per la lista Dati (sola lettura): risolve anche i campi FK nella loro
    /// etichetta, con lo stesso approccio a subquery già usato per i campi localizzati.
    /// Deve restare false per GetByIdAsync/QueryAsync: lì serve il valore grezzo (l'id),
    /// perché il form di editing lega l'input al valore reale e risolve l'etichetta
    /// separatamente via l'autocomplete AJAX (endpoint lookup/{fieldId}/label).
    /// </param>
    /// <summary>
    /// Risolve come leggere l'etichetta di una FK: se la colonna scelta come display è a
    /// sua volta localizzata (stesso pattern "udf_Localize" di WN_Contenuti/WN_Categorie/
    /// FFM.Squadre), serve un secondo salto attraverso la sua LocalizationSource — non
    /// basta leggerla direttamente. Riusato da BuildSelectExpression (lista Dati),
    /// GetLookupOptionsAsync e GetLookupLabelAsync (autocomplete), stessa logica ovunque.
    /// </summary>
    /// <param name="targetAlias">Alias SQL della tabella di destinazione della FK.</param>
    private static (string FromClause, string LabelExpression) BuildForeignKeyLabelSource(
        EntityDefinition target, string displayColumn, string targetAlias)
    {
        var targetTable = $"{QuoteIdentifier(target.SchemaName)}.{QuoteIdentifier(target.TableName)}";
        var displayField = target.Fields.FirstOrDefault(f =>
            string.Equals(f.ColumnName, displayColumn, StringComparison.OrdinalIgnoreCase));

        if (displayField is { IsLocalized: true, LocalizationSource: { } source })
        {
            var contentTable = $"{QuoteIdentifier(source.ContentSchemaName)}.{QuoteIdentifier(source.ContentTableName)}";
            var fromClause = $"""
                {targetTable} {targetAlias}
                INNER JOIN {contentTable} loc
                    ON loc.{QuoteIdentifier(source.ContentIdColumn)} = {targetAlias}.{QuoteIdentifier(displayColumn)}
                   AND loc.{QuoteIdentifier(source.LanguageIdColumn)} = {source.DefaultLanguageId}
                """;
            return (fromClause, $"loc.{QuoteIdentifier(source.TextColumn)}");
        }

        return ($"{targetTable} {targetAlias}", $"{targetAlias}.{QuoteIdentifier(displayColumn)}");
    }

    private static string BuildSelectExpression(FieldDefinition field, string tableAlias, bool resolveForeignKeys = false)
    {
        if (field.IsLocalized && field.LocalizationSource is { } source)
        {
            var contentTable = $"{QuoteIdentifier(source.ContentSchemaName)}.{QuoteIdentifier(source.ContentTableName)}";
            return $"""
                (SELECT TOP (1) loc.{QuoteIdentifier(source.TextColumn)}
                 FROM {contentTable} loc
                 WHERE loc.{QuoteIdentifier(source.ContentIdColumn)} = {tableAlias}.{QuoteIdentifier(field.ColumnName)}
                   AND loc.{QuoteIdentifier(source.LanguageIdColumn)} = {source.DefaultLanguageId}
                ) AS {QuoteIdentifier(field.ColumnName)}
                """;
        }

        if (resolveForeignKeys && field is { IsForeignKey: true, ForeignKeyTargetEntity: { } target, ForeignKeyDisplayColumn: { } displayColumn })
        {
            var (fromClause, labelExpression) = BuildForeignKeyLabelSource(target, displayColumn, "fk");
            return $"""
                (SELECT TOP (1) {labelExpression}
                 FROM {fromClause}
                 WHERE fk.{QuoteIdentifier(target.PrimaryKeyColumn)} = {tableAlias}.{QuoteIdentifier(field.ColumnName)}
                ) AS {QuoteIdentifier(field.ColumnName)}
                """;
        }

        return $"{tableAlias}.{QuoteIdentifier(field.ColumnName)} AS {QuoteIdentifier(field.ColumnName)}";
    }

    /// <summary>
    /// Legge, PRIMA dell'update, il valore grezzo (CONT_ID) attualmente salvato per i
    /// campi localizzati coinvolti — serve a capire se creare una nuova riga di
    /// traduzione o aggiornarne una esistente. Usa la stessa connection/transaction
    /// dell'update per coerenza.
    /// </summary>
    private static async Task<Dictionary<string, object?>> GetExistingLocalizedValuesAsync(
        SqlConnection connection, SqlTransaction transaction, EntityDefinition entity, FieldDefinition pkField,
        object id, List<FieldDefinition> fields, CancellationToken ct)
    {
        var localizedFields = fields.Where(f => f.IsLocalized).ToList();
        var result = new Dictionary<string, object?>();
        if (localizedFields.Count == 0)
        {
            return result;
        }

        var qualifiedTable = QualifiedTable(entity);
        var columns = string.Join(", ", localizedFields.Select(f => QuoteIdentifier(f.ColumnName)));

        var sql = $"SELECT {columns} FROM {qualifiedTable} WHERE {QuoteIdentifier(pkField.ColumnName)} = @Id;";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add(BuildParameter("@Id", pkField, id));

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            for (var i = 0; i < localizedFields.Count; i++)
            {
                var raw = reader.GetValue(i);
                result[localizedFields[i].ColumnName] = raw is DBNull ? null : raw;
            }
        }

        return result;
    }

    /// <summary>
    /// Risolve il valore da persistere per un campo. I campi localizzati e i campi File
    /// richiedono una risoluzione speciale (scrittura su una tabella/storage diversi
    /// prima di ottenere il valore finale da mettere nella colonna dell'entità).
    /// </summary>
    private async Task<object?> ResolveFieldValueAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        EntityDefinition entity,
        FieldDefinition field,
        IReadOnlyDictionary<string, string?> formValues,
        IReadOnlyDictionary<string, IFormFile?> files,
        object? existingContentId,
        CancellationToken ct)
    {
        if (field.IsLocalized)
        {
            var text = formValues.GetValueOrDefault(field.ColumnName);
            return await ResolveLocalizedValueAsync(connection, transaction, field, text, existingContentId, ct);
        }

        if (field.EditorType != EditorType.File)
        {
            return ConvertFormValue(field, formValues.GetValueOrDefault(field.ColumnName));
        }

        var file = files.GetValueOrDefault(field.ColumnName);
        if (file is null || file.Length == 0)
        {
            if (field.IsNullable)
            {
                return DBNull.Value;
            }
            throw new InvalidOperationException($"Il campo file '{field.ColumnName}' è obbligatorio.");
        }

        var isBinaryColumn = field.SqlDataType.ToLowerInvariant() is "varbinary" or "binary" or "image";
        if (isBinaryColumn)
        {
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            return ms.ToArray();
        }

        return await _fileStorage.SaveAsync(file, entity.TableName, ct);
    }

    /// <summary>
    /// Scrive/aggiorna la riga di traduzione nella LocalizationSource configurata (sempre
    /// nella lingua di default: nessun selettore multi-lingua per ora) e ritorna il
    /// CONT_ID da persistere nella colonna fisica dell'entità. Non elimina mai righe di
    /// traduzione esistenti, per non spezzare altre lingue eventualmente già tradotte
    /// sullo stesso CONT_ID.
    /// </summary>
    private static async Task<object?> ResolveLocalizedValueAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        FieldDefinition field,
        string? text,
        object? existingContentId,
        CancellationToken ct)
    {
        var source = field.LocalizationSource
            ?? throw new InvalidOperationException(
                $"Il campo '{field.ColumnName}' è marcato come localizzato ma non ha una LocalizationSource associata.");

        var hasExisting = existingContentId is not null and not DBNull;

        if (string.IsNullOrWhiteSpace(text))
        {
            if (field.IsNullable)
            {
                return DBNull.Value;
            }
            throw new InvalidOperationException($"Il campo '{field.DisplayName}' è obbligatorio.");
        }

        var contentTable = $"{QuoteIdentifier(source.ContentSchemaName)}.{QuoteIdentifier(source.ContentTableName)}";

        if (!hasExisting)
        {
            if (string.IsNullOrWhiteSpace(source.RowIdColumn))
            {
                throw new InvalidOperationException(
                    $"La sorgente di localizzazione '{source.DisplayName}' non ha configurato 'RowIdColumn' " +
                    "(es. LC_ID per WN_LOCALIZZAZIONE): è necessario per generare un nuovo contenuto tradotto. " +
                    "Configuralo dalla sezione Localizzazioni del backoffice.");
            }

            // Contenuto mai tradotto prima: inserisco la riga per la lingua di default e uso
            // l'id generato come CONT_ID, per convenzione (il CONT_ID di un contenuto nuovo
            // coincide con l'id della sua prima riga di traduzione).
            var insertSql = $"""
                INSERT INTO {contentTable} ({QuoteIdentifier(source.LanguageIdColumn)}, {QuoteIdentifier(source.TextColumn)})
                OUTPUT INSERTED.{QuoteIdentifier(source.RowIdColumn)}
                VALUES (@LanguageId, @Text);
                """;

            object newRowId;
            await using (var insertCommand = new SqlCommand(insertSql, connection, transaction))
            {
                insertCommand.Parameters.Add(new SqlParameter("@LanguageId", SqlDbType.Int) { Value = source.DefaultLanguageId });
                insertCommand.Parameters.Add(new SqlParameter("@Text", SqlDbType.NVarChar, -1) { Value = text });
                newRowId = await insertCommand.ExecuteScalarAsync(ct)
                    ?? throw new InvalidOperationException("Inserimento della traduzione non riuscito: nessun id generato.");
            }

            var updateContentIdSql = $"""
                UPDATE {contentTable}
                SET {QuoteIdentifier(source.ContentIdColumn)} = @ContentId
                WHERE {QuoteIdentifier(source.RowIdColumn)} = @RowId;
                """;
            await using (var updateCommand = new SqlCommand(updateContentIdSql, connection, transaction))
            {
                updateCommand.Parameters.Add(new SqlParameter("@ContentId", SqlDbType.Int) { Value = newRowId });
                updateCommand.Parameters.Add(new SqlParameter("@RowId", SqlDbType.Int) { Value = newRowId });
                await updateCommand.ExecuteNonQueryAsync(ct);
            }

            return newRowId;
        }

        // Contenuto già esistente: aggiorno la traduzione per la lingua di default se c'è
        // già una riga, altrimenti ne aggiungo una nuova riusando lo stesso CONT_ID.
        var rowIdColumn = source.RowIdColumn ?? source.ContentIdColumn;
        var existsSql = $"""
            SELECT {QuoteIdentifier(rowIdColumn)}
            FROM {contentTable}
            WHERE {QuoteIdentifier(source.ContentIdColumn)} = @ContentId
              AND {QuoteIdentifier(source.LanguageIdColumn)} = @LanguageId;
            """;

        object? existingRowId;
        await using (var existsCommand = new SqlCommand(existsSql, connection, transaction))
        {
            existsCommand.Parameters.Add(new SqlParameter("@ContentId", SqlDbType.Int) { Value = existingContentId });
            existsCommand.Parameters.Add(new SqlParameter("@LanguageId", SqlDbType.Int) { Value = source.DefaultLanguageId });
            existingRowId = await existsCommand.ExecuteScalarAsync(ct);
        }

        if (existingRowId is not null)
        {
            var updateSql = $"""
                UPDATE {contentTable}
                SET {QuoteIdentifier(source.TextColumn)} = @Text
                WHERE {QuoteIdentifier(rowIdColumn)} = @RowId;
                """;
            await using var updateCommand = new SqlCommand(updateSql, connection, transaction);
            updateCommand.Parameters.Add(new SqlParameter("@Text", SqlDbType.NVarChar, -1) { Value = text });
            updateCommand.Parameters.Add(new SqlParameter("@RowId", SqlDbType.Int) { Value = existingRowId });
            await updateCommand.ExecuteNonQueryAsync(ct);
        }
        else
        {
            var insertSql = $"""
                INSERT INTO {contentTable}
                    ({QuoteIdentifier(source.ContentIdColumn)}, {QuoteIdentifier(source.LanguageIdColumn)}, {QuoteIdentifier(source.TextColumn)})
                VALUES (@ContentId, @LanguageId, @Text);
                """;
            await using var insertCommand = new SqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.Add(new SqlParameter("@ContentId", SqlDbType.Int) { Value = existingContentId });
            insertCommand.Parameters.Add(new SqlParameter("@LanguageId", SqlDbType.Int) { Value = source.DefaultLanguageId });
            insertCommand.Parameters.Add(new SqlParameter("@Text", SqlDbType.NVarChar, -1) { Value = text });
            await insertCommand.ExecuteNonQueryAsync(ct);
        }

        return existingContentId;
    }

    private static object ConvertIdForLookup(FieldDefinition pkField, object rawId)
    {
        if (rawId is not string s)
        {
            return rawId;
        }

        return pkField.SqlDataType.ToLowerInvariant() switch
        {
            "int" => int.Parse(s, CultureInfo.InvariantCulture),
            "bigint" => long.Parse(s, CultureInfo.InvariantCulture),
            "smallint" => short.Parse(s, CultureInfo.InvariantCulture),
            "tinyint" => byte.Parse(s, CultureInfo.InvariantCulture),
            "uniqueidentifier" => Guid.Parse(s),
            _ => s
        };
    }

    /// <summary>Converte il valore stringa proveniente dal form nel tipo .NET/parametro SQL corretto.</summary>
    private static object? ConvertFormValue(FieldDefinition field, string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return field.IsNullable ? DBNull.Value : GetDefaultForNonNullable(field);
        }

        return field.SqlDataType.ToLowerInvariant() switch
        {
            "bit" => raw is "true" or "on" or "1",

            "int" => int.Parse(raw, CultureInfo.InvariantCulture),
            "bigint" => long.Parse(raw, CultureInfo.InvariantCulture),
            "smallint" => short.Parse(raw, CultureInfo.InvariantCulture),
            "tinyint" => byte.Parse(raw, CultureInfo.InvariantCulture),

            "decimal" or "numeric" or "money" or "smallmoney" => decimal.Parse(raw, CultureInfo.InvariantCulture),
            "float" => double.Parse(raw, CultureInfo.InvariantCulture),
            "real" => float.Parse(raw, CultureInfo.InvariantCulture),

            "date" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset" =>
                DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),

            "uniqueidentifier" => Guid.Parse(raw),

            "varbinary" or "binary" or "image" =>
                throw new NotSupportedException(
                    "Le colonne binarie vanno gestite tramite EditorType.File, non come testo."),

            _ => raw // varchar/nvarchar/char/nchar/text/ntext
        };
    }

    private static object GetDefaultForNonNullable(FieldDefinition field) => field.SqlDataType.ToLowerInvariant() switch
    {
        "bit" => false,
        "int" or "bigint" or "smallint" or "tinyint" => 0,
        "decimal" or "numeric" or "money" or "smallmoney" or "float" or "real" => 0m,
        _ => throw new InvalidOperationException(
            $"Il campo obbligatorio '{field.ColumnName}' non ha un valore e non è nullable.")
    };

    private static SqlParameter BuildParameter(string name, FieldDefinition field, object? value)
        => new(name, MapSqlDbType(field.SqlDataType)) { Value = value ?? DBNull.Value };

    private static SqlDbType MapSqlDbType(string sqlDataType) => sqlDataType.ToLowerInvariant() switch
    {
        "bit" => SqlDbType.Bit,
        "int" => SqlDbType.Int,
        "bigint" => SqlDbType.BigInt,
        "smallint" => SqlDbType.SmallInt,
        "tinyint" => SqlDbType.TinyInt,
        "decimal" or "numeric" => SqlDbType.Decimal,
        "money" => SqlDbType.Money,
        "smallmoney" => SqlDbType.SmallMoney,
        "float" => SqlDbType.Float,
        "real" => SqlDbType.Real,
        "date" => SqlDbType.Date,
        "datetime" => SqlDbType.DateTime,
        "datetime2" => SqlDbType.DateTime2,
        "smalldatetime" => SqlDbType.SmallDateTime,
        "datetimeoffset" => SqlDbType.DateTimeOffset,
        "uniqueidentifier" => SqlDbType.UniqueIdentifier,
        "varchar" => SqlDbType.VarChar,
        "nvarchar" => SqlDbType.NVarChar,
        "char" => SqlDbType.Char,
        "nchar" => SqlDbType.NChar,
        "text" => SqlDbType.Text,
        "ntext" => SqlDbType.NText,
        "varbinary" or "binary" or "image" => SqlDbType.VarBinary,
        _ => SqlDbType.NVarChar
    };
}