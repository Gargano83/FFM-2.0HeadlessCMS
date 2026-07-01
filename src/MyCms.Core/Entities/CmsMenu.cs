namespace MyCms.Core.Entities;

/// <summary>
/// Un menu di navigazione (es. "Menu principale", "Footer").
/// Un'app host può avere più menu distinti identificati per nome.
/// </summary>
public class CmsMenu
{
    public Guid Id { get; set; }

    /// <summary>Nome/chiave univoca del menu (es. "main-nav", "footer").</summary>
    public string Name { get; set; } = string.Empty;

    public ICollection<CmsMenuItem> Items { get; set; } = new List<CmsMenuItem>();
}
