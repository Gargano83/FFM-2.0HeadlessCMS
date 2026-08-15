namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>
/// Riga di <c>WN_Contenuti</c> per il blocco "ultimi articoli" in Homepage.
/// Equivalente del model <c>Articolo</c> legacy (Comunicazioni.cs), limitato ai
/// campi usati qui (endpoint legacy /api/comunicazioni/ultimiarticoli).
/// </summary>
public class LatestArticleViewModel
{
    public required string Titolo { get; init; }

    public string? Abstract { get; init; }

    public string? NomeCategoria { get; init; }

    public DateTime? Data { get; init; }

    /// <summary>Per il link "Leggi tutto" verso l'articolo specifico. Null se non disponibile (fallback all'elenco generico).</summary>
    public string? Slug { get; init; }
}
