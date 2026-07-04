using DAMIHeadlessCMS.Core.Enums;

namespace DAMIHeadlessCMS.Scaffolding.Models;

/// <summary>
/// Anteprima (non persistita) di un campo, così come sarebbe mostrato nel
/// wizard di scaffolding: dati strutturali letti a caldo da sys.columns +
/// eventuali personalizzazioni già presenti in cms.FieldDefinition se la
/// tabella era già stata scaffoldata in precedenza.
/// </summary>
public sealed record ScaffoldingPreviewField(
    string ColumnName,
    string SqlDataType,
    int? MaxLength,
    bool IsNullable,
    bool IsPrimaryKey,
    bool IsIdentity,
    bool IsForeignKey,
    string? ForeignKeyTarget,
    string DisplayName,
    EditorType EditorType,
    bool ShowInList,
    bool ShowInForm,
    bool IsRequired);

/// <summary>Anteprima (non persistita) di un'intera tabella/entità per il wizard.</summary>
public sealed record ScaffoldingPreviewEntity(
    string SchemaName,
    string TableName,
    Guid? EntityId,
    string DisplayName,
    string? Icon,
    bool IsNew,
    IReadOnlyList<ScaffoldingPreviewField> Fields)
{
    public string QualifiedName => $"{SchemaName}.{TableName}";
}