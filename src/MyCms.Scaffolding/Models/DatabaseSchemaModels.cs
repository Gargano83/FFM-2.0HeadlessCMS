namespace MyCms.Scaffolding.Models;

/// <summary>Tabella disponibile per lo scaffolding, così come letta da sys.tables.</summary>
public sealed record DatabaseTableInfo(string SchemaName, string TableName)
{
    public string QualifiedName => $"{SchemaName}.{TableName}";
}

/// <summary>Colonna di una tabella, così come letta da sys.columns/sys.types.</summary>
public sealed record DatabaseColumnInfo(
    string ColumnName,
    string SqlDataType,
    int? MaxLength,
    bool IsNullable,
    bool IsIdentity,
    bool IsPrimaryKey,
    int ColumnId);

/// <summary>Foreign key in uscita da una colonna, così come letta da sys.foreign_keys.</summary>
public sealed record DatabaseForeignKeyInfo(
    string ColumnName,
    string ReferencedSchema,
    string ReferencedTable,
    string ReferencedColumn);

/// <summary>Dettaglio completo di una tabella: colonne + foreign key in uscita.</summary>
public sealed record DatabaseTableDetails(
    DatabaseTableInfo Table,
    IReadOnlyList<DatabaseColumnInfo> Columns,
    IReadOnlyList<DatabaseForeignKeyInfo> ForeignKeys);
