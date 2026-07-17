namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>
/// Contenuto "hero" della Homepage, letto dalla riga di <c>WN_Contenuti</c>
/// configurata come documento Homepage (vedi <c>PublicSite:HomepageDocumentId</c>
/// in appsettings). Equivalente moderno di <c>Doc.Current</c> nel progetto legacy,
/// limitato ai soli campi usati in questo blocco.
/// </summary>
public class HeroContentViewModel
{
    public bool Found { get; init; }

    public string? Titolo { get; init; }

    public string? Abstract { get; init; }

    /// <summary>HTML del corpo (equivalente di <c>CO_Corpo</c> / <c>Doc.Current.Body</c>).</summary>
    public string? Corpo { get; init; }

    public static readonly HeroContentViewModel NotFound = new() { Found = false };
}
