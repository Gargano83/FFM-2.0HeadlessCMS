using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using MyCms.Core.Entities;
using MyCms.Core.Enums;
using System.Data;
using System.Globalization;

namespace MyCms.Admin.Data;

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
        EntityDefinition entity, int page, int pageSize, CancellationToken ct = default)
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
        var selectColumns = string.Join(", ", listFields.Select(f => QuoteIdentifier(f.ColumnName)));

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        int totalCount;
        await using (var countCmd = new SqlCommand($"SELECT COUNT(*) FROM {qualifiedTable};", connection))
        {
            totalCount = (int)(await countCmd.ExecuteScalarAsync(ct))!;
        }

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        var sql = $"""
            SELECT {selectColumns}
            FROM {qualifiedTable}
            ORDER BY {QuoteIdentifier(pkColumn.ColumnName)}
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
        var selectColumns = string.Join(", ", formFields.Select(f => QuoteIdentifier(f.ColumnName)));

        var sql = $"""
            SELECT {selectColumns}
            FROM {qualifiedTable}
            WHERE {QuoteIdentifier(pkField.ColumnName)} = @Id;
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

        await using var command = new SqlCommand(sql, connection);
        for (var i = 0; i < insertFields.Count; i++)
        {
            var field = insertFields[i];
            var value = field == pkField && explicitPkValue is not null
                ? explicitPkValue
                : await ResolveFieldValueAsync(entity, field, formValues, files, ct);
            command.Parameters.Add(BuildParameter($"@p{i}", field, value));
        }

        var result = await command.ExecuteScalarAsync(ct)
            ?? throw new InvalidOperationException("INSERT non ha restituito la chiave primaria generata.");
        return result;
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

        await using var command = new SqlCommand(sql, connection);
        for (var i = 0; i < updateFields.Count; i++)
        {
            var field = updateFields[i];
            var value = await ResolveFieldValueAsync(entity, field, formValues, files, ct);
            command.Parameters.Add(BuildParameter($"@p{i}", field, value));
        }
        command.Parameters.Add(BuildParameter("@Id", pkField, id));

        var affected = await command.ExecuteNonQueryAsync(ct);
        if (affected == 0)
        {
            throw new InvalidOperationException(
                $"Nessuna riga aggiornata in '{entity.QualifiedTableName}' per {pkField.ColumnName}={id}.");
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
        EntityDefinition targetEntity, string? displayColumn, string? searchText, CancellationToken ct = default)
    {
        var pkField = GetPrimaryKeyField(targetEntity);
        var labelColumnName = displayColumn ?? pkField.ColumnName;
        var qualifiedTable = QualifiedTable(targetEntity);

        var whereClause = string.IsNullOrWhiteSpace(searchText)
            ? ""
            : $"WHERE {QuoteIdentifier(labelColumnName)} LIKE @Search";

        var sql = $"""
            SELECT TOP (50) {QuoteIdentifier(pkField.ColumnName)}, {QuoteIdentifier(labelColumnName)}
            FROM {qualifiedTable}
            {whereClause}
            ORDER BY {QuoteIdentifier(labelColumnName)};
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            command.Parameters.Add(new SqlParameter("@Search", SqlDbType.NVarChar) { Value = $"%{searchText}%" });
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
        var qualifiedTable = QualifiedTable(targetEntity);

        var sql = $"""
            SELECT {QuoteIdentifier(labelColumnName)}
            FROM {qualifiedTable}
            WHERE {QuoteIdentifier(pkField.ColumnName)} = @Id;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(BuildParameter("@Id", pkField, ConvertIdForLookup(pkField, id)));

        var result = await command.ExecuteScalarAsync(ct);
        return result is null or DBNull ? null : result.ToString();
    }

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
    /// Risolve il valore da persistere per un campo. I campi EditorType.File sono
    /// gestiti qui separatamente da ConvertFormValue perché richiedono accesso a
    /// IFormFile (non solo alla stringa del form) e, per le colonne stringa,
    /// un'operazione asincrona di salvataggio su storage esterno.
    /// </summary>
    private async Task<object?> ResolveFieldValueAsync(
        EntityDefinition entity,
        FieldDefinition field,
        IReadOnlyDictionary<string, string?> formValues,
        IReadOnlyDictionary<string, IFormFile?> files,
        CancellationToken ct)
    {
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