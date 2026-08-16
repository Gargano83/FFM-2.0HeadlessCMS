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

        if (relativePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            relativePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return relativePath;
        }

        // Già migrato verso lo storage locale (vedi LegacyMediaMigrationService): servito
        // come file statico di QUESTO host, path assoluto rispetto alla root del sito —
        // non va prefissato con la base url del sito legacy, altrimenti risulterebbe
        // "https://.../media/files/uploads/..." invece di "/uploads/...".
        if (relativePath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase) ||
            relativePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return "/" + relativePath.TrimStart('/');
        }

        return $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }
}
