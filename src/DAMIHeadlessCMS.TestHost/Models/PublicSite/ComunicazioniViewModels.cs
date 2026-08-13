namespace DAMIHeadlessCMS.TestHost.Models.PublicSite;

/// <summary>Una categoria di comunicazioni (Regole/Reminder/Files/News/...), con conteggio articoli attivi.</summary>
public class ComunicazioneCategoryViewModel
{
    public required int Id { get; init; }

    public required string Nome { get; init; }

    public required string Slug { get; init; }

    public int NumeroDocumenti { get; init; }
}

/// <summary>Riga sintetica di un articolo nell'elenco (non il corpo completo).</summary>
public class ComunicazioneArticleSummaryViewModel
{
    public required string Titolo { get; init; }

    public string? Abstract { get; init; }

    public string? Slug { get; init; }

    public string? NomeCategoria { get; init; }

    public DateTime? Data { get; init; }
}

/// <summary>Indice/listing (usato sia per "tutte le categorie" sia per una categoria filtrata).</summary>
public class ComunicazioniListViewModel
{
    public IReadOnlyList<ComunicazioneCategoryViewModel> Categorie { get; init; } = [];

    public IReadOnlyList<ComunicazioneArticleSummaryViewModel> Articoli { get; init; } = [];

    public int Pagina { get; init; } = 1;

    public int TotalePagine { get; init; } = 1;

    /// <summary>Slug della categoria corrente, null se si sta mostrando "tutte".</summary>
    public string? CategoriaSlugCorrente { get; init; }

    public string? CategoriaNomeCorrente { get; init; }

    /// <summary>
    /// HTML del/dei blocco/i "html" di una CmsPage di supporto creata da backoffice
    /// (vedi ComunicazioniController, stesso pattern già usato per Statistiche —
    /// README §6.1), mostrato solo nell'elenco non filtrato (nessuna categoria
    /// selezionata): null quando si sta filtrando per categoria, o quando quella
    /// CmsPage non esiste/non è pubblicata.
    /// </summary>
    public string? IntroHtml { get; set; }
}

/// <summary>Pagina di dettaglio di un singolo articolo.</summary>
public class ComunicazioneArticoloViewModel
{
    public required string Titolo { get; init; }

    public string? NomeCategoria { get; init; }

    public string? CategoriaSlug { get; init; }

    public DateTime? Data { get; init; }

    /// <summary>HTML del corpo dell'articolo.</summary>
    public string? Corpo { get; init; }
}
