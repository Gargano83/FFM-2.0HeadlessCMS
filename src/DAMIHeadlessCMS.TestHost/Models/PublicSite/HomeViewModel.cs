namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>ViewModel composito della Homepage: un blocco per checkpoint (vedi docs/ROADMAP.md).</summary>
public class HomeViewModel
{
    public required HeroContentViewModel Hero { get; init; }

    public IReadOnlyList<TeamLogoViewModel> Teams { get; init; } = [];
}
