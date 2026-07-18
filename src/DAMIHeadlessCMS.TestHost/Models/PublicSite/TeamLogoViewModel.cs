namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>
/// Riga di <c>FFM.Squadre</c> per lo slider loghi in Homepage.
/// Equivalente del model <c>Squadre</c> legacy (Club.cs), limitato ai campi
/// usati dallo slider (endpoint legacy /api/club/squadre, query senza filtri).
/// </summary>
public class TeamLogoViewModel
{
    public required string Nome { get; init; }

    /// <summary>Path del logo così come salvato in colonna (relativo, da comporre con la base url dei file).</summary>
    public string? LogoPath { get; init; }
}
