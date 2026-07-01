using Microsoft.Data.SqlClient;
using MyCms.Scaffolding.Models;

namespace MyCms.Scaffolding;

public class SqlServerSchemaReader : ISqlServerSchemaReader
{
    private readonly string _connectionString;

    public SqlServerSchemaReader(string connectionString)
    {
        _connectionString = connectionString;
    }

    private const string ListTablesSql = """
        SELECT s.name AS SchemaName, t.name AS TableName
        FROM sys.tables t
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE t.is_ms_shipped = 0
          AND s.name <> 'cms'
          AND t.name <> '__EFMigrationsHistory'
        ORDER BY s.name, t.name;
        """;

    public async Task<IReadOnlyList<DatabaseTableInfo>> GetTablesAsync(CancellationToken ct = default)
    {
        var results = new List<DatabaseTableInfo>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(ListTablesSql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            results.Add(new DatabaseTableInfo(
                SchemaName: reader.GetString(reader.GetOrdinal("SchemaName")),
                TableName: reader.GetString(reader.GetOrdinal("TableName"))));
        }

        return results;
    }

    private const string ColumnsSql = """
        SELECT
            c.name AS ColumnName,
            ty.name AS SqlDataType,
            c.max_length AS MaxLengthBytes,
            c.is_nullable AS IsNullable,
            c.is_identity AS IsIdentity,
            CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey,
            c.column_id AS ColumnId
        FROM sys.columns c
        INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
        LEFT JOIN (
            SELECT ic.column_id
            FROM sys.index_columns ic
            INNER JOIN sys.indexes i
                ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            WHERE ic.object_id = OBJECT_ID(@QualifiedName) AND i.is_primary_key = 1
        ) pk ON pk.column_id = c.column_id
        WHERE c.object_id = OBJECT_ID(@QualifiedName)
        ORDER BY c.column_id;
        """;

    private const string ForeignKeysSql = """
        SELECT
            cpar.name AS ColumnName,
            OBJECT_SCHEMA_NAME(fk.referenced_object_id) AS ReferencedSchema,
            OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
            cref.name AS ReferencedColumn
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
        INNER JOIN sys.columns cpar
            ON fkc.parent_object_id = cpar.object_id AND fkc.parent_column_id = cpar.column_id
        INNER JOIN sys.columns cref
            ON fkc.referenced_object_id = cref.object_id AND fkc.referenced_column_id = cref.column_id
        WHERE fk.parent_object_id = OBJECT_ID(@QualifiedName);
        """;

    public async Task<DatabaseTableDetails> GetTableDetailsAsync(string schemaName, string tableName, CancellationToken ct = default)
    {
        var table = new DatabaseTableInfo(schemaName, tableName);
        var qualifiedName = $"[{schemaName.Replace("]", "]]")}].[{tableName.Replace("]", "]]")}]";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var columns = new List<DatabaseColumnInfo>();
        await using (var command = new SqlCommand(ColumnsSql, connection))
        {
            command.Parameters.AddWithValue("@QualifiedName", qualifiedName);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var sqlDataType = reader.GetString(reader.GetOrdinal("SqlDataType"));
                var maxLengthBytes = reader.GetInt16(reader.GetOrdinal("MaxLengthBytes"));

                columns.Add(new DatabaseColumnInfo(
                    ColumnName: reader.GetString(reader.GetOrdinal("ColumnName")),
                    SqlDataType: sqlDataType,
                    MaxLength: ComputeMaxLength(sqlDataType, maxLengthBytes),
                    IsNullable: reader.GetBoolean(reader.GetOrdinal("IsNullable")),
                    IsIdentity: reader.GetBoolean(reader.GetOrdinal("IsIdentity")),
                    IsPrimaryKey: reader.GetInt32(reader.GetOrdinal("IsPrimaryKey")) == 1,
                    ColumnId: reader.GetInt32(reader.GetOrdinal("ColumnId"))));
            }
        }

        var foreignKeys = new List<DatabaseForeignKeyInfo>();
        await using (var command = new SqlCommand(ForeignKeysSql, connection))
        {
            command.Parameters.AddWithValue("@QualifiedName", qualifiedName);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                foreignKeys.Add(new DatabaseForeignKeyInfo(
                    ColumnName: reader.GetString(reader.GetOrdinal("ColumnName")),
                    ReferencedSchema: reader.GetString(reader.GetOrdinal("ReferencedSchema")),
                    ReferencedTable: reader.GetString(reader.GetOrdinal("ReferencedTable")),
                    ReferencedColumn: reader.GetString(reader.GetOrdinal("ReferencedColumn"))));
            }
        }

        return new DatabaseTableDetails(table, columns, foreignKeys);
    }

    /// <summary>
    /// sys.columns.max_length è in byte: per nvarchar/nchar va diviso per 2
    /// per ottenere il numero di caratteri; -1 indica MAX (nvarchar(max) ecc.).
    /// </summary>
    private static int? ComputeMaxLength(string sqlDataType, short maxLengthBytes)
    {
        if (maxLengthBytes == -1)
        {
            return null;
        }

        return sqlDataType is "nvarchar" or "nchar"
            ? maxLengthBytes / 2
            : maxLengthBytes;
    }
}
