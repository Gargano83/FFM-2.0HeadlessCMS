namespace MyCms.Core.Entities;

/// <summary>
/// Pagina custom gestita dal CMS. Il contenuto è un JSON a blocchi
/// (vedi <see cref="ContentJson"/>) che l'app host interpreta e renderizza
/// con la tecnologia front-end scelta.
/// </summary>
public class CmsPage
{
    public Guid Id { get; set; }

    /// <summary>Percorso univoco della pagina (es. "chi-siamo").</summary>
    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public Guid? ParentId { get; set; }
    public CmsPage? Parent { get; set; }

    /// <summary>
    /// Contenuto strutturato a blocchi, serializzato JSON. Esempio di blocco:
    /// { "type": "html", "html": "..." } oppure
    /// { "type": "component", "tag": "app-widget", "config": { ... } } oppure
    /// { "type": "entityList", "entity": "Products" }
    /// </summary>
    public string ContentJson { get; set; } = "[]";

    public bool IsPublished { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
