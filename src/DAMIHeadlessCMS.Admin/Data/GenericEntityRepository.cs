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
        EntityDefinition entity, int page, int pageSize, bool resolveForeignKeys = false,
        IReadOnlyDictionary<string, string>? filterValues = null, CancellationToken ct = default)
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

        var filters = BuildListFilters(entity, filterValues);
        var (whereClause, _, parameters) = BuildWhereAndOrderBy(entity, filters, sort: null, alias);

        // COUNT(*) OVER() in coda al SELECT: stesso approccio di QueryPageAsync, una sola
        // query invece di un COUNT(*) separato — evita anche di dover duplicare i parametri
        // del filtro su due SqlCommand distinti (un SqlParameter non è riusabile su più
        // SqlCommand contemporaneamente).
        var sql = $"""
            SELECT {selectColumns}, COUNT(*) OVER() AS __TotalCount
            FROM {qualifiedTable} {alias}
            {whereClause}
            ORDER BY {alias}.{QuoteIdentifier(pkColumn.ColumnName)}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Offset", SqlDbType.Int) { Value = (page - 1) * pageSize });
        command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize });
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        var totalCount = 0;
        var columnNames = listFields.Select(f => f.ColumnName).ToList();

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(ReadRow(reader, columnNames));
            totalCount = reader.GetInt32(columnNames.Count);
        }

        return new GenericEntityPage(rows, totalCount, page, pageSize);
    }

    /// <summary>
    /// Converte i filtri grezzi (colonna -&gt; testo dal form) in QueryFilter tipizzati, in
    /// base a EditorType — vedi il commento su IGenericEntityRepository.GetListAsync per
    /// la semantica completa (Contains per testo, uguaglianza per numeri/checkbox/FK,
    /// intervallo sul giorno per Date/DateTime).
    /// </summary>
    private static List<QueryFilter> BuildListFilters(EntityDefinition entity, IReadOnlyDictionary<string, string>? filterValues)
    {
        var filters = new List<QueryFilter>();
        if (filterValues is null || filterValues.Count == 0)
        {
            return filters;
        }

        foreach (var (columnName, raw) in filterValues)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var field = entity.Fields.FirstOrDefault(f => string.Equals(f.ColumnName, columnName, StringComparison.OrdinalIgnoreCase));
            if (field is null || field.EditorType is EditorType.File or EditorType.Hidden or EditorType.Password)
            {
                continue;
            }

            try
            {
                switch (field.EditorType)
                {
                    case EditorType.Checkbox when !field.IsLocalized:
                        filters.Add(new QueryFilter(field.ColumnName, QueryFilterOperator.Equal, raw == "true"));
                        break;

                    case EditorType.Date or EditorType.DateTime when !field.IsLocalized:
                        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue))
                        {
                            var dayStart = dateValue.Date;
                            filters.Add(new QueryFilter(field.ColumnName, QueryFilterOperator.GreaterThanOrEqual, dayStart));
                            filters.Add(new QueryFilter(field.ColumnName, QueryFilterOperator.LessThan, dayStart.AddDays(1)));
                        }
                        break;

                    case EditorType.Number or EditorType.Select when !field.IsLocalized:
                        // Copre anche il caso di una relazione FK configurata con EditorType
                        // diverso da Select (es. mostrato come Numero): uguaglianza esatta
                        // sulla colonna fisica, corretta a prescindere dall'editor scelto.
                        filters.Add(new QueryFilter(field.ColumnName, QueryFilterOperator.Equal, ConvertFormValue(field, raw)));
                        break;

                    default:
                        // Text/TextArea/RichText, incluso il caso localizzato (il valore fisico
                        // è una chiave, non il testo: BuildWhereAndOrderBy risolve il testo
                        // tradotto via join solo per Contains, mai per gli altri operatori —
                        // ecco perché i rami sopra escludono esplicitamente i campi localizzati
                        // invece di lasciarli cadere qui per errore).
                        filters.Add(new QueryFilter(field.ColumnName, QueryFilterOperator.Contains, raw));
                        break;
                }
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                // Valore non convertibile al tipo della colonna (es. testo non numerico o
                // fuori range digitato per errore in un campo Number): il filtro viene
                // ignorato invece di far fallire l'intera lista.
            }
        }

        return filters;
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
        // Stessa logica per un campo Password lasciato vuoto: non è "cancella la
        // password", è "non toccarla" (l'hash esistente non viene mai rimandato
        // al browser, quindi vuoto è l'unico stato possibile per "nessuna modifica").
        var updateFields = entity.Fields
            .Where(f => f.ShowInForm && !f.IsPrimaryKey)
            .Where(f => f.EditorType != EditorType.File || files.GetValueOrDefault(f.ColumnName) is { Length: > 0 })
            .Where(f => f.EditorType != EditorType.Password || !string.IsNullOrEmpty(formValues.GetValueOrDefault(f.ColumnName)))
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

        var selectFields = entity.Fields.OrderBy(f => f.SortOrder).ToList();
        var qualifiedTable = QualifiedTable(entity);
        const string alias = "t";
        var selectColumns = string.Join(", ", selectFields.Select(f => BuildSelectExpression(f, alias)));

        var (whereClause, orderByClause, parameters) = BuildWhereAndOrderBy(entity, filters, sort, alias);

        var sql = $"""
            SELECT TOP (@Top) {selectColumns}
            FROM {qualifiedTable} {alias}
            {whereClause}
            ORDER BY {orderByClause};
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

    public async Task<GenericEntityPage> QueryPageAsync(
        EntityDefinition entity,
        IReadOnlyList<QueryFilter>? filters,
        IReadOnlyList<QuerySort>? sort,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var selectFields = entity.Fields.OrderBy(f => f.SortOrder).ToList();
        var qualifiedTable = QualifiedTable(entity);
        const string alias = "t";
        // COUNT(*) OVER() in coda al SELECT: stesso approccio della query legacy
        // (Blog_Articles), un'unica query invece di una separata per il totale.
        // Va DOPO le colonne mappate, cosi la posizione delle colonne attese da
        // ReadRow (per indice, non per nome) resta invariata.
        var selectColumns = string.Join(", ", selectFields.Select(f => BuildSelectExpression(f, alias)));

        var (whereClause, orderByClause, parameters) = BuildWhereAndOrderBy(entity, filters, sort, alias);

        var sql = $"""
            SELECT {selectColumns}, COUNT(*) OVER() AS __TotalCount
            FROM {qualifiedTable} {alias}
            {whereClause}
            ORDER BY {orderByClause}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Offset", SqlDbType.Int) { Value = (page - 1) * pageSize });
        command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize });
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        var totalCount = 0;
        var columnNames = selectFields.Select(f => f.ColumnName).ToList();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(ReadRow(reader, columnNames));
            totalCount = reader.GetInt32(columnNames.Count);
        }

        return new GenericEntityPage(rows, totalCount, page, pageSize);
    }

    /// <summary>Costruisce WHERE (filtri in AND) e ORDER BY, condivisi da QueryAsync/QueryPageAsync.</summary>
    private static (string WhereClause, string OrderByClause, List<SqlParameter> Parameters) BuildWhereAndOrderBy(
        EntityDefinition entity, IReadOnlyList<QueryFilter>? filters, IReadOnlyList<QuerySort>? sort, string alias)
    {
        filters ??= [];
        sort ??= [];

        var whereClauses = new List<string>();
        var parameters = new List<SqlParameter>();
        for (var i = 0; i < filters.Count; i++)
        {
            var filter = filters[i];
            var field = FindFilterableField(entity, filter.ColumnName, filter.Operator);
            var paramName = $"@f{i}";

            if (filter.Operator == QueryFilterOperator.Contains && field.IsLocalized && field.LocalizationSource is { } source)
            {
                // Il valore fisico della colonna è una chiave (l'id della riga di
                // localizzazione), non il testo: per "contiene" va risolto il testo
                // tradotto e confrontato quello, non la chiave — stessa join usata da
                // BuildSelectExpression per mostrarlo in elenco, qui come EXISTS invece
                // che come SELECT scalare (più efficiente: basta sapere se esiste una
                // riga che soddisfa la LIKE, non leggerne il valore).
                var contentTable = $"{QuoteIdentifier(source.ContentSchemaName)}.{QuoteIdentifier(source.ContentTableName)}";
                whereClauses.Add($"""
                    EXISTS (
                        SELECT 1 FROM {contentTable} loc
                        WHERE loc.{QuoteIdentifier(source.ContentIdColumn)} = {alias}.{QuoteIdentifier(field.ColumnName)}
                          AND loc.{QuoteIdentifier(source.LanguageIdColumn)} = {source.DefaultLanguageId}
                          AND loc.{QuoteIdentifier(source.TextColumn)} LIKE {paramName}
                    )
                    """);
                parameters.Add(new SqlParameter(paramName, SqlDbType.NVarChar) { Value = $"%{filter.Value}%" });
            }
            else if (filter.Operator == QueryFilterOperator.Contains)
            {
                // LIKE con pattern costruito qui in C#, mai concatenato nel testo SQL: resta
                // comunque un parametro vero, solo con % aggiunti al valore.
                whereClauses.Add($"{alias}.{QuoteIdentifier(field.ColumnName)} LIKE {paramName}");
                parameters.Add(new SqlParameter(paramName, SqlDbType.NVarChar) { Value = $"%{filter.Value}%" });
            }
            else
            {
                whereClauses.Add($"{alias}.{QuoteIdentifier(field.ColumnName)} {SqlOperator(filter.Operator)} {paramName}");
                parameters.Add(BuildParameter(paramName, field, filter.Value));
            }
        }

        var orderByClauses = sort
            .Select(s => $"{alias}.{QuoteIdentifier(FindFilterableField(entity, s.ColumnName).ColumnName)}{(s.Descending ? " DESC" : "")}")
            .ToList();
        // Se non viene richiesto un ordinamento esplicito, ordina comunque per PK per un
        // risultato deterministico (stesso criterio "minimo" usato da GetListAsync).
        // Per QueryPageAsync è anche un requisito T-SQL: OFFSET/FETCH richiede ORDER BY.
        if (orderByClauses.Count == 0)
        {
            orderByClauses.Add($"{alias}.{QuoteIdentifier(GetPrimaryKeyField(entity).ColumnName)}");
        }

        var whereClause = whereClauses.Count == 0 ? "" : $"WHERE {string.Join(" AND ", whereClauses)}";
        return (whereClause, string.Join(", ", orderByClauses), parameters);
    }

    public async Task<object?> FindIdByLocalizedValueAsync(
        EntityDefinition entity, string columnName, string value, CancellationToken ct = default)
    {
        var field = entity.Fields.FirstOrDefault(f =>
            string.Equals(f.ColumnName, columnName, StringComparison.OrdinalIgnoreCase));

        if (field is not { IsLocalized: true, LocalizationSource: not null })
        {
            throw new InvalidOperationException(
                $"'{columnName}' non è un campo localizzato di '{entity.QualifiedTableName}': " +
                "FindIdByLocalizedValueAsync richiede un campo localizzato.");
        }

        var pkField = GetPrimaryKeyField(entity);
        var (fromClause, labelExpression) = BuildForeignKeyLabelSource(entity, columnName, "t");

        var sql = $"""
            SELECT TOP (1) t.{QuoteIdentifier(pkField.ColumnName)}
            FROM {fromClause}
            WHERE {labelExpression} = @Value;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Value", SqlDbType.NVarChar) { Value = value });

        var result = await command.ExecuteScalarAsync(ct);
        return result is null or DBNull ? null : result;
    }

    /// <summary>
    /// Risolve un FieldDefinition per nome colonna, valido come filtro/ordinamento di
    /// QueryAsync: deve esistere. Un campo localizzato è ammesso SOLO se l'operatore è
    /// Contains (vedi il ramo dedicato in BuildWhereAndOrderBy, che risolve il testo
    /// tradotto tramite join invece di confrontare la chiave fisica) — per qualunque
    /// altro operatore, o per l'ordinamento (forOperator: null), resta vietato: un
    /// confronto diretto sulla chiave non avrebbe senso.
    /// </summary>
    private static FieldDefinition FindFilterableField(EntityDefinition entity, string columnName, QueryFilterOperator? forOperator = null)
    {
        var field = entity.Fields.FirstOrDefault(f => string.Equals(f.ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"'{columnName}' non è un campo scaffoldato di '{entity.QualifiedTableName}'.");

        if (field.IsLocalized && forOperator != QueryFilterOperator.Contains)
        {
            throw new InvalidOperationException(
                $"'{columnName}' è un campo localizzato: non può essere usato come filtro diretto con " +
                "l'operatore richiesto, né come ordinamento (il valore fisico è una chiave, non il testo " +
                "tradotto) — solo l'operatore Contains è ammesso, e risolve il testo tradotto via join.");
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

        if (field.EditorType == EditorType.Password)
        {
            return await ResolvePasswordValueAsync(connection, transaction, field, formValues.GetValueOrDefault(field.ColumnName), ct);
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
    /// Trasforma il testo in chiaro inserito nel form nel valore effettivo da
    /// scrivere sulla colonna password, MAI in chiaro. In arrivo qui il valore
    /// è sempre non vuoto: un campo Password lasciato vuoto in modifica viene
    /// già escluso a monte dal SET (vedi UpdateAsync); in creazione, vuoto
    /// significa "nessun valore fornito" e va comunque tramite questo metodo
    /// per applicare correttamente il vincolo di obbligatorietà.
    /// </summary>
    private static async Task<object?> ResolvePasswordValueAsync(
        SqlConnection connection, SqlTransaction transaction, FieldDefinition field, string? raw, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(raw))
        {
            if (field.IsNullable)
            {
                return DBNull.Value;
            }
            throw new InvalidOperationException($"Il campo '{field.DisplayName}' è obbligatorio.");
        }

        if (!string.IsNullOrWhiteSpace(field.PasswordHashFunction))
        {
            // Funzione SQL scalare configurata dall'amministratore nel wizard di
            // scaffolding (metadato fidato, non input utente): l'hashing avviene
            // lato database per riprodurre esattamente il valore che produrrebbe
            // una funzione legacy già in uso altrove (es. in fase di login),
            // evitando qualunque scostamento tra un'implementazione .NET e quella
            // T-SQL originale.
            var sql = $"SELECT {field.PasswordHashFunction}(@PlainValue);";
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.Add(new SqlParameter("@PlainValue", SqlDbType.NVarChar, 255) { Value = raw });
            var result = await command.ExecuteScalarAsync(ct);
            return result is null or DBNull ? DBNull.Value : result;
        }

        // Nessuna funzione legacy configurata: hashing SHA-512 calcolato in .NET,
        // stesso formato testuale "0x" + esadecimale maiuscolo prodotto da
        // CONVERT(varchar, HASHBYTES('SHA2_512', ...), 1) — ma senza dipendere dal
        // database, adatto a colonne password create ex novo dal CMS.
        var hashBytes = System.Security.Cryptography.SHA512.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return "0x" + Convert.ToHexString(hashBytes);
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