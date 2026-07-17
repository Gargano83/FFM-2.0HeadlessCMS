using DAMIHeadlessCMS.Core.Entities;
using DAMIHeadlessCMS.Core.Enums;

namespace DAMIHeadlessCMS.TestHost.PublicSite;

/// <summary>
/// Nodo di menu già risolto in un Url navigabile, pronto per il rendering.
/// Costruito a partire da <see cref="CmsMenuItem"/> (vedi <see cref="MenuUrlResolver"/>).
/// </summary>
public class MenuNode
{
    public required string Label { get; init; }
    public required string Url { get; init; }
    public bool OpenInNewTab { get; init; }
    public List<MenuNode> Children { get; init; } = [];
}

/// <summary>
/// Risolve il Url navigabile di una <see cref="CmsMenuItem"/> in base al suo
/// <see cref="MenuTargetType"/>. Il CMS conserva solo i metadati (vedi CmsMenuItem);
/// questa risoluzione a Url concreto è responsabilità del progetto host.
/// </summary>
public static class MenuUrlResolver
{
    public static string Resolve(CmsMenuItem item) => item.TargetType switch
    {
        MenuTargetType.Page => $"/{item.TargetValue.TrimStart('/')}",
        MenuTargetType.ExternalUrl => item.TargetValue,
        // Elenco pubblico di un'entità scaffoldata: non ancora implementato in questa
        // fase (nessuna pagina pubblica generica di listing/dettaglio per i record
        // scaffoldati). Verrà risolto quando implementeremo quel caso d'uso.
        MenuTargetType.Entity => "#",
        _ => "#"
    };

    /// <summary>Costruisce l'albero di <see cref="MenuNode"/> da una lista piatta di CmsMenuItem.</summary>
    public static List<MenuNode> BuildTree(IReadOnlyList<CmsMenuItem> flatItems)
    {
        var byParent = flatItems
            .OrderBy(i => i.SortOrder)
            .ToLookup(i => i.ParentId);

        List<MenuNode> Build(Guid? parentId) => byParent[parentId]
            .Select(i => new MenuNode
            {
                Label = i.Label,
                Url = Resolve(i),
                OpenInNewTab = i.OpenInNewTab,
                Children = Build(i.Id)
            })
            .ToList();

        return Build(null);
    }
}
