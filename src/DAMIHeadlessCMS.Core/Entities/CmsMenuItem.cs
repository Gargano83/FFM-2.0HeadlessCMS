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

    /// <summary>
    /// Se true, il progetto host dovrebbe renderizzare il link con target="_blank"
    /// (nuova scheda). Configurabile per singola voce, indipendentemente dal
    /// TargetType: utile ad es. per link a documentazione/regolamento esterni
    /// alla SPA/app host, ma disponibile per qualunque tipo di destinazione.
    /// Il CMS si limita a generare l'alberatura: il rendering effettivo del
    /// menu (e quindi l'uso pratico di questo flag) è responsabilità dell'host.
    /// </summary>
    public bool OpenInNewTab { get; set; }

    public int SortOrder { get; set; }

    public ICollection<CmsMenuItem> Children { get; set; } = new List<CmsMenuItem>();
}
