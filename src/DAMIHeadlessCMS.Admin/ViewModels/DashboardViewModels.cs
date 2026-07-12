using DAMIHeadlessCMS.Core.Entities;

namespace DAMIHeadlessCMS.Admin.ViewModels;

/// <summary>
/// Dati per la dashboard post-login (vista Index di GenericEntityController,
/// route "/dami"): oltre all'elenco di entità gestite già presente dalla fase
/// 3, contatori riepilogativi, log di audit recente e pagine modificate di
/// recente — vedi fase 14 della ROADMAP.
/// </summary>
public class DashboardViewModel
{
    public IReadOnlyList<EntityDefinition> Entities { get; set; } = [];

    public DashboardCounters Counters { get; set; } = new();

    /// <summary>Ultime righe di <see cref="AuditLogEntry"/>, più recenti prima.</summary>
    public IReadOnlyList<AuditLogEntry> RecentAuditEntries { get; set; } = [];

    /// <summary>Ultime CmsPage create o modificate, più recenti prima.</summary>
    public IReadOnlyList<CmsPage> RecentPages { get; set; } = [];
}

public class DashboardCounters
{
    public int ScaffoldedEntities { get; set; }
    public int Pages { get; set; }
    public int PublishedPages { get; set; }
    public int MenuItems { get; set; }
    public int AdminUsers { get; set; }
    public int EditorUsers { get; set; }
    public int OperatorUsers { get; set; }
}
