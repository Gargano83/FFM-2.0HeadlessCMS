using DAMIHeadlessCMS.Core.Enums;

namespace DAMIHeadlessCMS.Admin.ViewModels;

// --- Step 1: elenco tabelle disponibili ---

public sealed record ScaffoldingTableItem(string SchemaName, string TableName, bool AlreadyScaffolded);

public sealed class ScaffoldingTableListViewModel
{
    public required IReadOnlyList<ScaffoldingTableItem> Tables { get; init; }
}

// --- Payload inviato dal wizard in fase di salvataggio finale ---

public sealed class ScaffoldingSaveRequest
{
    public List<ScaffoldingSaveEntity> Entities { get; init; } = new();
}

public sealed class ScaffoldingSaveEntity
{
    public string SchemaName { get; init; } = string.Empty;
    public string TableName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public List<ScaffoldingSaveField> Fields { get; init; } = new();
}

public sealed class ScaffoldingSaveField
{
    public string ColumnName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public EditorType EditorType { get; init; }
    public bool ShowInList { get; init; }
    public bool ShowInForm { get; init; }
    public bool IsRequired { get; init; }
}