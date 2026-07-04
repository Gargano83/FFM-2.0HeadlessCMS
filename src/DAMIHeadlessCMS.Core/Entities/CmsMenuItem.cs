using DAMIHeadlessCMS.Core.Enums;

namespace DAMIHeadlessCMS.Core.Entities;

/// <summary>
/// Voce di un menu di navigazione, con struttura ad albero tramite <see cref="ParentId"/>.
/// </summary>
public class CmsMenuItem
{
    public Guid Id { get; set; }

    public Guid MenuId { get; set; }
    public CmsMenu? Menu { get; set; }

    public Guid? ParentId { get; set; }
    public CmsMenuItem? Parent { get; set; }

    public string Label { get; set; } = string.Empty;

    public MenuTargetType TargetType { get; set; }

    /// <summary>
    /// Valore della destinazione: Slug della CmsPage se TargetType = Page,
    /// TableName dell'EntityDefinition se TargetType = Entity, URL se ExternalUrl.
    /// </summary>
    public string TargetValue { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public ICollection<CmsMenuItem> Children { get; set; } = new List<CmsMenuItem>();
}
