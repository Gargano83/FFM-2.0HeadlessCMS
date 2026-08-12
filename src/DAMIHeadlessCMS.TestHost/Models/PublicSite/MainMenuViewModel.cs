using DAMIHeadlessCMS.TestHost.PublicSite;

namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>Modello per <see cref="ViewComponents.MainMenuViewComponent"/>.</summary>
public sealed class MainMenuViewModel
{
    public IReadOnlyList<MenuNode> Items { get; init; } = [];

    /// <summary>
    /// True se l'utente corrente è autenticato con lo schema PublicUser (area
    /// riservata) — non riflette in alcun modo un eventuale login al backoffice
    /// (/dami, schema Identity separato). Determina se l'icona nel menu punta al
    /// login o direttamente all'area riservata.
    /// </summary>
    public bool IsPublicUserAuthenticated { get; init; }
}
