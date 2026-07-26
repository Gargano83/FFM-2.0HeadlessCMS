using DAMIHeadlessCMS.TestHost.Models.PublicSite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DAMIHeadlessCMS.TestHost.PublicSite;

/// <summary>
/// Punto unico per i dati della pagina Statistiche: carica una volta per richiesta le
/// "tabelle di supporto" (FFM.Squadre, WN_LOOKUP) che servono a praticamente ogni widget
/// della pagina, evitando le tante piccole letture per-id già usate per l'Albo d'oro della
/// Homepage — qui, con ~20 sezioni sulla stessa pagina, converrebbe farne troppe.
/// Registrato Scoped: le cache vivono per la durata della singola richiesta HTTP.
/// </summary>
public class StatisticheDataService
{
    private readonly LegacyContentReader _content;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StatisticheDataService> _logger;

    private IReadOnlyDictionary<int, TeamRef>? _teamCache;
    private IReadOnlyDictionary<int, (string Label, int Order)>? _lookupCache;
    private IReadOnlyList<IReadOnlyDictionary<string, object?>>? _riepilogoRowsCache;

    public StatisticheDataService(LegacyContentReader content, IConfiguration configuration, ILogger<StatisticheDataService> logger)
    {
        _content = content;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Id di competizione configurati (PublicSite:Competizioni:*), stesso significato di "statistiche" in utils.js del client legacy.</summary>
    public int? GetCompetitionId(string key) => _configuration.GetValue<int?>($"PublicSite:Competizioni:{key}");

    private async Task<IReadOnlyDictionary<int, TeamRef>> GetTeamsAsync(CancellationToken ct)
    {
        if (_teamCache is not null)
        {
            return _teamCache;
        }

        var entity = await _content.GetEntityAsync("FFM", "Squadre", ct);
        if (entity is null)
        {
            _logger.LogWarning("FFM.Squadre non risulta ancora scaffoldata: i riferimenti a squadra nella pagina Statistiche saranno vuoti.");
            _teamCache = new Dictionary<int, TeamRef>();
            return _teamCache;
        }

        var baseUrl = _configuration["PublicSite:LegacyFileBaseUrl"] ?? string.Empty;
        var rows = await _content.GetAllRowsAsync(entity, ct: ct);

        _teamCache = rows
            .Where(r => r.GetValueOrDefault("Id") is not null)
            .ToDictionary(
                r => Convert.ToInt32(r.GetValueOrDefault("Id")),
                r => new TeamRef(
                    r.GetValueOrDefault("Nome") as string ?? string.Empty,
                    LegacyMediaUrlResolver.ResolveLogoUrl(r.GetValueOrDefault("LogoStatistiche") as string, baseUrl)));

        return _teamCache;
    }

    private async Task<IReadOnlyDictionary<int, (string Label, int Order)>> GetLookupsAsync(CancellationToken ct)
    {
        if (_lookupCache is not null)
        {
            return _lookupCache;
        }

        var entity = await _content.GetEntityAsync("dbo", "WN_LOOKUP", ct);
        if (entity is null)
        {
            _logger.LogWarning("WN_LOOKUP non risulta ancora scaffoldata: le etichette (stagioni, ecc.) nella pagina Statistiche saranno vuote.");
            _lookupCache = new Dictionary<int, (string, int)>();
            return _lookupCache;
        }

        // WN_LOOKUP è condivisa da molte liste diverse: letta tutta in un colpo solo,
        // molto più efficiente delle tante GetRowByIdAsync fatte per l'Albo d'oro della
        // Homepage, qui con molte più sezioni sulla stessa pagina.
        var rows = await _content.GetAllRowsAsync(entity, maxRows: 2000, ct: ct);

        _lookupCache = rows
            .Where(r => r.GetValueOrDefault("LK_ID") is not null)
            .ToDictionary(
                r => Convert.ToInt32(r.GetValueOrDefault("LK_ID")),
                r => (
                    Label: r.GetValueOrDefault("LK_Valore") as string ?? string.Empty,
                    Order: Convert.ToInt32(r.GetValueOrDefault("LK_ORDINE") ?? 0)));

        return _lookupCache;
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetRiepilogoRowsAsync(CancellationToken ct)
    {
        if (_riepilogoRowsCache is not null)
        {
            return _riepilogoRowsCache;
        }

        var entity = await _content.GetEntityAsync("FFM", "RiepilogoStatistiche", ct);
        _riepilogoRowsCache = entity is null
            ? []
            : await _content.GetAllRowsAsync(entity, maxRows: 2000, ct: ct);

        return _riepilogoRowsCache;
    }

    /// <summary>
    /// Tabella "Titoli" per una competizione: quante volte ogni squadra l'ha vinta e in
    /// quali stagioni. Derivata da FFM.RiepilogoStatistiche (già usata per l'Albo d'oro
    /// della Homepage) — nessuna nuova tabella da scaffoldare, stessa fonte dati.
    /// </summary>
    public async Task<TitleTableViewModel?> BuildTitlesTableAsync(int competitionId, string competitionLabel, CancellationToken ct)
    {
        var rows = await GetRiepilogoRowsAsync(ct);
        if (rows.Count == 0)
        {
            return null;
        }

        var teams = await GetTeamsAsync(ct);
        var lookups = await GetLookupsAsync(ct);

        var titleRows = rows
            .Where(r => (r.GetValueOrDefault("Competizione") as int?) == competitionId)
            .GroupBy(r => r.GetValueOrDefault("Squadra") as int? ?? 0)
            .Where(g => g.Key > 0)
            .Select(g =>
            {
                var team = teams.GetValueOrDefault(g.Key);
                var seasons = g
                    .Select(r => r.GetValueOrDefault("Stagione") as int? ?? 0)
                    .Distinct()
                    .Select(id => (Id: id, Info: lookups.GetValueOrDefault(id)))
                    .OrderBy(x => x.Info.Order)
                    .Select(x => string.IsNullOrEmpty(x.Info.Label) ? $"#{x.Id}" : x.Info.Label);

                return new TitleRowViewModel
                {
                    TeamName = team?.Name ?? $"#{g.Key}",
                    LogoPath = team?.LogoPath,
                    TitleCount = g.Count(),
                    Seasons = string.Join(", ", seasons)
                };
            })
            .OrderByDescending(t => t.TitleCount)
            .ThenBy(t => t.TeamName)
            .ToList();

        return titleRows.Count == 0 ? null : new TitleTableViewModel { CompetitionLabel = competitionLabel, Rows = titleRows };
    }

    /// <summary>
    /// Risultati per la famiglia "standard" di competizioni (Vincitore/Finalista perdente/
    /// Risultato/Sede finale) — stessa forma per Poppa Campioni, Coppa delle Poppe, Poppa di
    /// Lega: un solo metodo, parametrizzato sul nome tabella (schema FFM).
    /// </summary>
    public async Task<IReadOnlyList<StandardResultRowViewModel>> GetStandardCompetitionResultsAsync(
        string tableName, CancellationToken ct)
    {
        var entity = await _content.GetEntityAsync("FFM", tableName, ct);
        if (entity is null)
        {
            _logger.LogWarning("FFM.{Table} non risulta ancora scaffoldata: la sezione viene omessa.", tableName);
            return [];
        }

        var teams = await GetTeamsAsync(ct);
        var lookups = await GetLookupsAsync(ct);
        var rows = await _content.GetAllRowsAsync(entity, ct: ct);

        return rows
            .Select(r =>
            {
                var seasonId = r.GetValueOrDefault("Stagione") as int? ?? 0;
                var seasonInfo = lookups.GetValueOrDefault(seasonId);
                return new StandardResultRowViewModel
                {
                    SeasonLabel = string.IsNullOrEmpty(seasonInfo.Label) ? $"#{seasonId}" : seasonInfo.Label,
                    SeasonOrder = seasonInfo.Order,
                    Vincitore = teams.GetValueOrDefault(r.GetValueOrDefault("Vincitore") as int? ?? 0),
                    FinalistaPerdente = teams.GetValueOrDefault(r.GetValueOrDefault("FinalistaPerdente") as int? ?? 0),
                    Risultato = r.GetValueOrDefault("Risultato") as string,
                    SedeFinale = teams.GetValueOrDefault(r.GetValueOrDefault("SedeFinale") as int? ?? 0),
                    SedeFinaleStadio = r.GetValueOrDefault("SedeFinaleStadio") as string
                };
            })
            .OrderBy(r => r.SeasonOrder)
            .ToList();
    }

    /// <summary>
    /// Risultati Campionato (FFM.CampionatoStatistiche): solo Primo/Secondo/Terzo, come
    /// mostrato dal client legacy (la tabella fisica arriva fino al dodicesimo posto con
    /// relativi punti, non usati in questo widget).
    /// </summary>
    public async Task<IReadOnlyList<CampionatoResultRowViewModel>> GetCampionatoResultsAsync(CancellationToken ct)
    {
        var entity = await _content.GetEntityAsync("FFM", "CampionatoStatistiche", ct);
        if (entity is null)
        {
            _logger.LogWarning("FFM.CampionatoStatistiche non risulta ancora scaffoldata: la tabella risultati Campionato viene omessa.");
            return [];
        }

        var teams = await GetTeamsAsync(ct);
        var lookups = await GetLookupsAsync(ct);
        var rows = await _content.GetAllRowsAsync(entity, ct: ct);

        return rows
            .Select(r =>
            {
                var seasonId = r.GetValueOrDefault("Stagione") as int? ?? 0;
                var seasonInfo = lookups.GetValueOrDefault(seasonId);
                return new CampionatoResultRowViewModel
                {
                    SeasonLabel = string.IsNullOrEmpty(seasonInfo.Label) ? $"#{seasonId}" : seasonInfo.Label,
                    SeasonOrder = seasonInfo.Order,
                    Primo = teams.GetValueOrDefault(r.GetValueOrDefault("Primo") as int? ?? 0),
                    Secondo = teams.GetValueOrDefault(r.GetValueOrDefault("Secondo") as int? ?? 0),
                    Terzo = teams.GetValueOrDefault(r.GetValueOrDefault("Terzo") as int? ?? 0)
                };
            })
            .OrderBy(r => r.SeasonOrder)
            .ToList();
    }
}
