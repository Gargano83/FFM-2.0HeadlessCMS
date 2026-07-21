using System.Diagnostics;
using DAMIHeadlessCMS.Admin.Data;
using DAMIHeadlessCMS.TestHost.Models;
using DAMIHeadlessCMS.TestHost.Models.PublicSite;
using DAMIHeadlessCMS.TestHost.PublicSite;
using Microsoft.AspNetCore.Mvc;

namespace DAMIHeadlessCMS.TestHost.Controllers;

/// <summary>
/// Pagina pubblica Homepage (vedi docs/ROADMAP.md, migrazione pagine legacy —
/// checkpoint "Menu + Hero"). Il blocco hero corrisponde a <c>Doc.Current</c>
/// nel progetto legacy: la riga di <c>WN_Contenuti</c> il cui id è configurato
/// in <c>PublicSite:HomepageDocumentId</c> (equivalente di WebConst.HOMEPAGE_ID).
/// </summary>
public class HomeController : Controller
{
    private readonly LegacyContentReader _content;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HomeController> _logger;

    public HomeController(LegacyContentReader content, IConfiguration configuration, ILogger<HomeController> logger)
    {
        _content = content;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var hero = await LoadHeroAsync(ct);
        var teams = await LoadTeamsAsync(ct);
        var articles = await LoadLatestArticlesAsync(ct);
        var hallOfFame = await LoadHallOfFameAsync(ct);

        return View(new HomeViewModel { Hero = hero, Teams = teams, LatestArticles = articles, HallOfFame = hallOfFame });
    }

    private async Task<HeroContentViewModel> LoadHeroAsync(CancellationToken ct)
    {
        var homepageDocumentId = _configuration.GetValue("PublicSite:HomepageDocumentId", 1);

        var entity = await _content.GetEntityAsync("dbo", "WN_Contenuti", ct);
        if (entity is null)
        {
            _logger.LogWarning(
                "WN_Contenuti non risulta ancora scaffoldata: la Homepage viene mostrata senza contenuto hero. " +
                "Scaffoldala da /dami/scaffolding per popolare questo blocco.");
            return HeroContentViewModel.NotFound;
        }

        var row = await _content.GetRowByIdAsync(entity, homepageDocumentId, ct);
        if (row is null)
        {
            _logger.LogWarning(
                "Nessuna riga trovata in WN_Contenuti per id={HomepageDocumentId} (PublicSite:HomepageDocumentId).",
                homepageDocumentId);
            return HeroContentViewModel.NotFound;
        }

        return new HeroContentViewModel
        {
            Found = true,
            Titolo = row.GetValueOrDefault("co_titolo") as string,
            Abstract = row.GetValueOrDefault("co_abstract") as string,
            Corpo = row.GetValueOrDefault("co_corpo") as string
        };
    }

    private async Task<IReadOnlyList<TeamLogoViewModel>> LoadTeamsAsync(CancellationToken ct)
    {
        var entity = await _content.GetEntityAsync("FFM", "Squadre", ct);
        if (entity is null)
        {
            _logger.LogWarning(
                "FFM.Squadre non risulta ancora scaffoldata: lo slider squadre viene omesso. " +
                "Scaffoldala da /dami/scaffolding per popolare questo blocco.");
            return [];
        }

        var baseUrl = _configuration["PublicSite:LegacyFileBaseUrl"] ?? string.Empty;
        var rows = await _content.GetAllRowsAsync(entity, ct: ct);

        return rows
            .Select(row => new TeamLogoViewModel
            {
                Nome = row.GetValueOrDefault("Nome") as string ?? string.Empty,
                LogoPath = ResolveLogoUrl(row.GetValueOrDefault("LogoStatistiche") as string, baseUrl)
            })
            .Where(t => !string.IsNullOrWhiteSpace(t.Nome))
            .ToList();
    }

    private static string? ResolveLogoUrl(string? relativePath, string baseUrl)
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

    private async Task<IReadOnlyList<LatestArticleViewModel>> LoadLatestArticlesAsync(CancellationToken ct)
    {
        var articleDocTypeId = _configuration.GetValue<int?>("PublicSite:ArticleDocTypeId");
        if (articleDocTypeId is null)
        {
            _logger.LogWarning(
                "PublicSite:ArticleDocTypeId non configurato: il blocco ultimi articoli viene omesso. " +
                "Valorizzalo con l'id corrispondente a WebConst.COMUNICAZIONE_ARTICOLO_TPD nel progetto legacy.");
            return [];
        }

        var contentEntity = await _content.GetEntityAsync("dbo", "WN_Contenuti", ct);
        if (contentEntity is null)
        {
            return [];
        }

        var rows = await _content.GetFilteredRowsAsync(
            contentEntity,
            filters:
            [
                new QueryFilter("co_tipo_doc", QueryFilterOperator.Equal, articleDocTypeId.Value),
                new QueryFilter("co_attivo", QueryFilterOperator.Equal, true)
            ],
            sort:
            [
                new QuerySort("co_data_inizio", Descending: true),
                new QuerySort("co_id", Descending: true)
            ],
            top: 6,
            ct: ct);

        var categoryEntity = await _content.GetEntityAsync("dbo", "WN_Categorie", ct);

        var articles = new List<LatestArticleViewModel>();
        foreach (var row in rows)
        {
            string? categoryName = null;
            if (categoryEntity is not null && row.GetValueOrDefault("co_categoria") is { } categoryId)
            {
                var categoryRow = await _content.GetRowByIdAsync(categoryEntity, categoryId, ct);
                categoryName = categoryRow?.GetValueOrDefault("ca_nome") as string;
            }

            articles.Add(new LatestArticleViewModel
            {
                Titolo = row.GetValueOrDefault("co_titolo") as string ?? string.Empty,
                Abstract = row.GetValueOrDefault("co_abstract") as string,
                NomeCategoria = categoryName,
                Data = row.GetValueOrDefault("co_data_inizio") as DateTime?
            });
        }

        return articles;
    }

    private async Task<HallOfFameViewModel?> LoadHallOfFameAsync(CancellationToken ct)
    {
        var statsEntity = await _content.GetEntityAsync("FFM", "RiepilogoStatistiche", ct);
        var lookupEntity = await _content.GetEntityAsync("dbo", "WN_LOOKUP", ct);
        var teamEntity = await _content.GetEntityAsync("FFM", "Squadre", ct);
        if (statsEntity is null || lookupEntity is null || teamEntity is null)
        {
            _logger.LogWarning(
                "FFM.RiepilogoStatistiche / WN_LOOKUP / FFM.Squadre non risultano tutte scaffoldate: " +
                "il blocco Albo d'oro viene omesso.");
            return null;
        }

        var rows = await _content.GetAllRowsAsync(statsEntity, ct: ct);
        if (rows.Count == 0)
        {
            return null;
        }

        // Cache per evitare di risolvere più volte lo stesso id di lookup/squadra
        // (le stesse stagioni/competizioni/squadre ricorrono su più righe).
        var lookupCache = new Dictionary<int, (string Label, int Order)>();
        var teamCache = new Dictionary<int, string>();

        async Task<(string Label, int Order)> ResolveLookupAsync(int id)
        {
            if (lookupCache.TryGetValue(id, out var cached))
            {
                return cached;
            }

            var row = await _content.GetRowByIdAsync(lookupEntity, id, ct);
            var result = (
                Label: row?.GetValueOrDefault("LK_Valore") as string ?? $"#{id}",
                Order: row?.GetValueOrDefault("LK_ORDINE") as int? ?? 0);
            lookupCache[id] = result;
            return result;
        }

        async Task<string> ResolveTeamNameAsync(int id)
        {
            if (teamCache.TryGetValue(id, out var cached))
            {
                return cached;
            }

            var row = await _content.GetRowByIdAsync(teamEntity, id, ct);
            var name = row?.GetValueOrDefault("Nome") as string ?? $"#{id}";
            teamCache[id] = name;
            return name;
        }

        var seasons = new Dictionary<int, (string Label, int Order)>();
        var competitions = new Dictionary<int, (string Label, int Order)>();
        var cells = new Dictionary<(int Season, int Competition), string>();

        foreach (var row in rows)
        {
            var seasonId = row.GetValueOrDefault("Stagione") as int? ?? 0;
            var competitionId = row.GetValueOrDefault("Competizione") as int? ?? 0;
            var teamId = row.GetValueOrDefault("Squadra") as int? ?? 0;
            if (seasonId <= 0 || competitionId <= 0)
            {
                continue;
            }

            seasons.TryAdd(seasonId, await ResolveLookupAsync(seasonId));
            competitions.TryAdd(competitionId, await ResolveLookupAsync(competitionId));
            cells[(seasonId, competitionId)] = teamId > 0 ? await ResolveTeamNameAsync(teamId) : string.Empty;
        }

        if (seasons.Count == 0 || competitions.Count == 0)
        {
            return null;
        }

        // Ordinamento sia delle stagioni (righe) sia delle competizioni (colonne) per
        // LK_ORDINE — lo stesso criterio usato dalla query legacy (ORDER BY ..., LK_ORDINE),
        // ma qui applicato dinamicamente a qualsiasi competizione presente nei dati.
        var orderedCompetitions = competitions.OrderBy(c => c.Value.Order).ToList();
        var orderedSeasons = seasons.OrderBy(s => s.Value.Order).ToList();

        var hallOfFameRows = orderedSeasons
            .Select(season => new HallOfFameRow
            {
                SeasonLabel = season.Value.Label,
                Teams = orderedCompetitions
                    .Select(competition => cells.GetValueOrDefault((season.Key, competition.Key)))
                    .ToList()
            })
            .ToList();

        return new HallOfFameViewModel
        {
            CompetitionNames = orderedCompetitions.Select(c => c.Value.Label).ToList(),
            Rows = hallOfFameRows
        };
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
