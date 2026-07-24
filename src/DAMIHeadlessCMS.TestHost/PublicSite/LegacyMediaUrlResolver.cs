namespace DAMIHeadlessCMS.TestHost.PublicSite;

/// <summary>
/// Compone l'url di un file legacy (es. logo squadra) a partire dal path relativo salvato
/// in colonna, con la stessa logica usata sia dalla Homepage sia dalla pagina Statistiche
/// (path già assoluto → lasciato invariato, altrimenti prefissato con la base url legacy).
/// </summary>
public static class LegacyMediaUrlResolver
{
    public static string? ResolveLogoUrl(string? relativePath, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        return relativePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               relativePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? relativePath
            : $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }
}
