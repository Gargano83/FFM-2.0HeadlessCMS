namespace DAMIHeadlessCMS.Admin.Utilities;

/// <summary>
/// Normalizzazione e confronto dei percorsi "interni" del sito: quelli che il
/// CMS può davvero garantire univoci, perché ne conosce entrambe le fonti —
/// <c>CmsPage.Slug</c> (voci di menu <c>TargetType = Page</c>, sempre risolte
/// da una dropdown alimentata dalle pagine esistenti, quindi già al sicuro) e
/// <c>CmsMenuItem.TargetValue</c> per <c>TargetType = ExternalUrl</c> quando il
/// valore è un percorso relativo tipo "/regolamento" (testo libero, nessuna
/// validazione pregressa).
///
/// I link davvero esterni (<c>http://</c>, <c>https://</c>, <c>mailto:</c>,
/// percorsi protocol-relative <c>//host/...</c>, ecc.) sono intenzionalmente
/// esclusi dal controllo: non competono per lo spazio di URL gestito dal CMS,
/// e includerli produrrebbe solo falsi positivi (es. due voci di menu che
/// linkano legittimamente lo stesso sito esterno).
///
/// Non copre invece gli URL "di dettaglio" per righe di tabelle applicative
/// scaffoldate (es. una futura pagina categoria/documento su WN_CATEGORIE/
/// WN_DOCUMENTI): il CMS non ha oggi alcun concetto di routing per singolo
/// record, quindi non c'è nulla su cui applicare un controllo di unicità.
/// Vedi la nota "in ipotesi" nella ROADMAP per come si potrebbe estendere in
/// futuro, quando questa esigenza diventerà concreta.
/// </summary>
public static class InternalUrlPath
{
    /// <summary>
    /// True se il valore è un percorso interno del sito su cui ha senso
    /// applicare l'unicità: inizia con "/" ma non è protocol-relative
    /// ("//host/..."). Qualsiasi altro formato (URL assoluto con schema,
    /// stringa vuota, ecc.) è considerato "esterno" e ignorato dal controllo.
    /// </summary>
    public static bool IsInternal(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.StartsWith('/')
           && !value.StartsWith("//", StringComparison.Ordinal);

    /// <summary>
    /// Normalizza un percorso interno per il confronto di unicità: trim dello
    /// spazio, "/" iniziale garantito, "/" finale rimosso (tranne per la root
    /// "/"). Confronto sempre case-sensitive a valle (gli URL lo sono per
    /// specifica) — usare <see cref="StringComparer.Ordinal"/>.
    /// </summary>
    public static string Normalize(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        if (trimmed.Length > 1 && trimmed.EndsWith('/'))
        {
            trimmed = trimmed.TrimEnd('/');
        }

        return trimmed;
    }

    /// <summary>Percorso interno equivalente allo slug di una CmsPage (es. "chi-siamo" → "/chi-siamo").</summary>
    public static string FromPageSlug(string slug) => Normalize("/" + slug.Trim());
}
