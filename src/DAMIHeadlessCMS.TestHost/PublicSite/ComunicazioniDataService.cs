using DAMIHeadlessCMS.Admin.Data;
using DAMIHeadlessCMS.Core.Entities;
using DAMIHeadlessCMS.TestHost.Models.PublicSite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DAMIHeadlessCMS.TestHost.PublicSite;

/// <summary>
/// Punto unico per i dati della pagina Comunicazioni (indice categorie/articoli + dettaglio
/// articolo). Legge <c>WN_Contenuti</c>/<c>WN_Categorie</c>, già scaffoldate per la Homepage
/// (checkpoint "ultimi articoli") — qui in più con paginazione reale e risoluzione di
/// slug/url leggibili, entrambi campi localizzati.
/// </summary>
public class ComunicazioniDataService
{
    private const int ArticoliPerPagina = 10;

    private readonly LegacyContentReader _content;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ComunicazioniDataService> _logger;

    public ComunicazioniDataService(LegacyContentReader content, IConfiguration configuration, ILogger<ComunicazioniDataService> logger)
    {
        _content = content;
        _configuration = configuration;
        _logger = logger;
    }

    private int? ArticleDocTypeId => _configuration.GetValue<int?>("PublicSite:ArticleDocTypeId");

    /// <summary>
    /// Slug delle categorie da mostrare come sottosezioni di Comunicazioni
    /// (PublicSite:ComunicazioniCategorieSlugs) — WN_Categorie contiene l'intero albero
    /// di categorie del sito (anche di altre sezioni, es. Homepage/Statistiche), non solo
    /// quelle di Comunicazioni: nel legacy il filtro era sulla gerarchia di ca_ordine
    /// (fragile, specifica dell'installazione), qui una lista esplicita e configurabile.
    /// </summary>
    private IReadOnlyList<string> ComunicazioniCategorieSlugs =>
        _configuration.GetSection("PublicSite:ComunicazioniCategorieSlugs").Get<string[]>() ?? [];

    /// <summary>Elenco categorie con conteggio articoli attivi (per i tab dell'indice).</summary>
    public async Task<IReadOnlyList<ComunicazioneCategoryViewModel>> GetCategoriesAsync(CancellationToken ct)
    {
        var categoriesEntity = await _content.GetEntityAsync("dbo", "WN_Categorie", ct);
        var contentEntity = await _content.GetEntityAsync("dbo", "WN_Contenuti", ct);
        if (categoriesEntity is null || contentEntity is null || ArticleDocTypeId is not int articleDocTypeId)
        {
            LogMissingConfig();
            return [];
        }

        var allowedSlugs = ComunicazioniCategorieSlugs;
        var categoryRows = (await _content.GetAllRowsAsync(categoriesEntity, ct: ct))
            .Where(row => allowedSlugs.Contains(row.GetValueOrDefault("ca_url") as string, StringComparer.OrdinalIgnoreCase));

        var categories = new List<ComunicazioneCategoryViewModel>();
        foreach (var row in categoryRows)
        {
            var categoryId = row.GetValueOrDefault("ca_id") as int? ?? 0;
            if (categoryId <= 0)
            {
                continue;
            }

            var count = await CountActiveArticlesAsync(contentEntity, categoryId, articleDocTypeId, ct);
            categories.Add(new ComunicazioneCategoryViewModel
            {
                Id = categoryId,
                Nome = row.GetValueOrDefault("ca_nome") as string ?? string.Empty,
                Slug = row.GetValueOrDefault("ca_url") as string ?? string.Empty,
                NumeroDocumenti = count
            });
        }

        return categories;
    }

    private async Task<int> CountActiveArticlesAsync(EntityDefinition contentEntity, int categoryId, int articleDocTypeId, CancellationToken ct)
    {
        var page = await _content.GetFilteredPageAsync(
            contentEntity,
            filters:
            [
                new QueryFilter("co_categoria", QueryFilterOperator.Equal, categoryId),
                new QueryFilter("co_tipo_doc", QueryFilterOperator.Equal, articleDocTypeId),
                new QueryFilter("co_attivo", QueryFilterOperator.Equal, true)
            ],
            sort: null,
            page: 1,
            pageSize: 1,
            ct: ct);

        return page.TotalCount;
    }

    /// <summary>
    /// Elenco paginato di articoli, opzionalmente filtrato per categoria (null = tutte).
    /// </summary>
    public async Task<ComunicazioniListViewModel> GetArticlesPageAsync(
        ComunicazioneCategoryViewModel? category, int page, CancellationToken ct)
    {
        var contentEntity = await _content.GetEntityAsync("dbo", "WN_Contenuti", ct);
        if (contentEntity is null || ArticleDocTypeId is not int articleDocTypeId)
        {
            LogMissingConfig();
            return new ComunicazioniListViewModel();
        }

        var filters = new List<QueryFilter>
        {
            new("co_tipo_doc", QueryFilterOperator.Equal, articleDocTypeId),
            new("co_attivo", QueryFilterOperator.Equal, true)
        };
        if (category is not null)
        {
            filters.Add(new QueryFilter("co_categoria", QueryFilterOperator.Equal, category.Id));
        }

        var result = await _content.GetFilteredPageAsync(
            contentEntity,
            filters,
            sort: [new QuerySort("co_data_inizio", Descending: true), new QuerySort("co_id", Descending: true)],
            page: page,
            pageSize: ArticoliPerPagina,
            ct: ct);

        var articles = result.Rows
            .Select(row => new ComunicazioneArticleSummaryViewModel
            {
                Titolo = row.GetValueOrDefault("co_titolo") as string ?? string.Empty,
                Abstract = row.GetValueOrDefault("co_abstract") as string,
                Slug = row.GetValueOrDefault("co_url") as string,
                NomeCategoria = category?.Nome,
                Data = row.GetValueOrDefault("co_data_inizio") as DateTime?
            })
            .ToList();

        return new ComunicazioniListViewModel
        {
            Categorie = await GetCategoriesAsync(ct),
            Articoli = articles,
            Pagina = result.Page,
            TotalePagine = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)ArticoliPerPagina)),
            CategoriaSlugCorrente = category?.Slug,
            CategoriaNomeCorrente = category?.Nome
        };
    }

    /// <summary>Risolve un articolo dal suo slug/url leggibile (co_url, campo localizzato).</summary>
    public async Task<ComunicazioneArticoloViewModel?> TryGetArticleBySlugAsync(string slug, CancellationToken ct)
    {
        var contentEntity = await _content.GetEntityAsync("dbo", "WN_Contenuti", ct);
        if (contentEntity is null)
        {
            LogMissingConfig();
            return null;
        }

        var id = await _content.FindIdBySlugAsync(contentEntity, "co_url", slug, ct);
        if (id is null)
        {
            return null;
        }

        var row = await _content.GetRowByIdAsync(contentEntity, id, ct);
        if (row is null)
        {
            return null;
        }

        var categoryId = row.GetValueOrDefault("co_categoria") as int? ?? 0;
        string? categoryName = null;
        string? categorySlug = null;
        if (categoryId > 0)
        {
            var categories = await GetCategoriesAsync(ct);
            var category = categories.FirstOrDefault(c => c.Id == categoryId);
            categoryName = category?.Nome;
            categorySlug = category?.Slug;
        }

        return new ComunicazioneArticoloViewModel
        {
            Titolo = row.GetValueOrDefault("co_titolo") as string ?? string.Empty,
            NomeCategoria = categoryName,
            CategoriaSlug = categorySlug,
            Data = row.GetValueOrDefault("co_data_inizio") as DateTime?,
            Corpo = row.GetValueOrDefault("co_corpo") as string
        };
    }

    private void LogMissingConfig() => _logger.LogWarning(
        "WN_Contenuti/WN_Categorie non risultano scaffoldate, o PublicSite:ArticleDocTypeId non configurato: " +
        "la pagina Comunicazioni viene mostrata vuota.");
}
