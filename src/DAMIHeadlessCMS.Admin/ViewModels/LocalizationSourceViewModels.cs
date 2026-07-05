using System.ComponentModel.DataAnnotations;

namespace DAMIHeadlessCMS.Admin.ViewModels;

public sealed class LocalizationSourceListItemViewModel
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string ContentTable { get; init; } = string.Empty;
    public string LanguageTable { get; init; } = string.Empty;
    public int DefaultLanguageId { get; init; }
    public int UsedByFieldsCount { get; init; }
}

public sealed class LocalizationSourceFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Il nome è obbligatorio.")]
    public string DisplayName { get; set; } = string.Empty;

    [Required] public string ContentSchemaName { get; set; } = "dbo";
    [Required] public string ContentTableName { get; set; } = string.Empty;
    [Required] public string ContentIdColumn { get; set; } = string.Empty;
    [Required] public string LanguageIdColumn { get; set; } = string.Empty;
    [Required] public string TextColumn { get; set; } = string.Empty;
    public string? RowIdColumn { get; set; }

    [Required] public string LanguageSchemaName { get; set; } = "dbo";
    [Required] public string LanguageTableName { get; set; } = string.Empty;
    [Required] public string LanguageIdColumnInLanguageTable { get; set; } = string.Empty;
    public string? LanguageCodeColumn { get; set; }
    public string? LanguageNameColumn { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Deve essere un id lingua valido.")]
    public int DefaultLanguageId { get; set; } = 1;

    public bool IsEdit => Id.HasValue;
}