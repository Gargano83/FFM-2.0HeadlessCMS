using DAMIHeadlessCMS.TestHost.PublicSite;

namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>Modello per <see cref="ViewComponents.MainMenuViewComponent"/>.</summary>
public sealed class MainMenuViewModel
{
    public IReadOnlyList<MenuNode> Items { get; init; } = [];

    /// <summary>
    /// True se l'utente corrente è autenticato con lo schema PublicUser (area
    /// riservata) — non riflette in alcun modo un eventuale login al backoffice
    /// (/dami, schema Identity separato). Determina se l'icona "area riservata" nel
    /// menu punta al login o direttamente all'area riservata.
    /// </summary>
    public bool IsPublicUserAuthenticated { get; init; }

    /// <summary>
    /// True se l'utente corrente è autenticato con lo schema Identity del backoffice
    /// (/dami) — indipendente da <see cref="IsPublicUserAuthenticated"/>: le due
    /// utenze sono completamente separate (WN_Utenti vs ASP.NET Core Identity), una
    /// persona può essere autenticata su una, sull'altra, su entrambe o su nessuna.
    /// Determina se l'icona "backoffice" nel menu punta al login o alla dashboard.
    /// </summary>
    public bool IsBackofficeAuthenticated { get; init; }
}
