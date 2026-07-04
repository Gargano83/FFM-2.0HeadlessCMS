using System.ComponentModel.DataAnnotations;

namespace MyCms.Admin.ViewModels;

public sealed class PageListItemViewModel
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public Guid? ParentId { get; init; }
    public string? ParentTitle { get; init; }
    public bool IsPublished { get; init; }
    public int SortOrder { get; init; }
}

public sealed class PageFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Lo slug è obbligatorio.")]
    [RegularExpression("^[a-z0-9-]+(/[a-z0-9-]+)*$",
        ErrorMessage = "Usa solo lettere minuscole, numeri, trattini e '/' per percorsi annidati.")]
    public string Slug { get; set; } = string.Empty;

    [Required(ErrorMessage = "Il titolo è obbligatorio.")]
    public string Title { get; set; } = string.Empty;

    public Guid? ParentId { get; set; }

    public bool IsPublished { get; set; }

    public int SortOrder { get; set; }

    public string ContentJson { get; set; } = "[]";

    public List<PageParentOption> ParentOptions { get; set; } = new();

    public List<PageEntityOption> AvailableEntities { get; set; } = new();

    public bool IsEdit => Id.HasValue;
}

public sealed record PageParentOption(Guid Id, string Title);

public sealed record PageEntityOption(string QualifiedName, string DisplayName);