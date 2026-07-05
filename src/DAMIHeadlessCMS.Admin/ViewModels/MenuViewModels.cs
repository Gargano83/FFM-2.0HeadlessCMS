using DAMIHeadlessCMS.Core.Enums;

namespace DAMIHeadlessCMS.Admin.ViewModels;

public sealed class MenuListItemViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int ItemCount { get; init; }
}

public sealed class MenuEditorViewModel
{
    public Guid MenuId { get; init; }
    public string MenuName { get; init; } = string.Empty;

    /// <summary>Albero corrente già serializzato come JSON, pronto per l'idratazione lato client.</summary>
    public string ItemsJson { get; init; } = "[]";

    public List<MenuPageOption> AvailablePages { get; init; } = new();
    public List<MenuEntityOption> AvailableEntities { get; init; } = new();
}

public sealed record MenuPageOption(string Slug, string Title);
public sealed record MenuEntityOption(string QualifiedName, string DisplayName);

// --- Payload di salvataggio dell'intero albero (POST JSON) ---

public sealed class MenuSaveRequest
{
    public List<MenuSaveItem> Items { get; init; } = new();
}

public sealed class MenuSaveItem
{
    /// <summary>Id generato lato client (per nodi esistenti = Guid originale in stringa, per nuovi = "new-N").</summary>
    public string ClientId { get; init; } = string.Empty;
    public string? ParentClientId { get; init; }
    public string Label { get; init; } = string.Empty;
    public MenuTargetType TargetType { get; init; }
    public string TargetValue { get; init; } = string.Empty;
    public bool OpenInNewTab { get; init; }
    public int SortOrder { get; init; }
}