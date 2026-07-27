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

    /// <summary>
    /// Etichetta stagione dell'eccezione storica "non disputata" di SuperPoppa di Lega
    /// (equivalente di exception_match in utils.js del client legacy).
    /// </summary>
    public string? GetNonDisputataSeasonLabel() => _configuration["PublicSite:SuperpoppaDiLegaStagioneNonDisputata"];

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
    /// Risultati per la famiglia "doppia sorgente" (il vincitore proviene da una delle due
    /// squadre sorgente, es. vincitrice Campionato vs vincitrice Poppa di Lega) — stessa
    /// forma per SuperPoppa di Lega, SuperPoppa Europea, Poppa Intercontinentale: cambiano
    /// solo i nomi delle due colonne sorgente. <paramref name="nonDisputataSeasonLabel"/>
    /// gestisce l'eccezione storica di SuperPoppa di Lega (stagione senza partita disputata).
    /// </summary>
    public async Task<IReadOnlyList<DualSourceResultRowViewModel>> GetDualSourceCompetitionResultsAsync(
        string tableName, string sourceAColumn, string sourceBColumn, string? nonDisputataSeasonLabel, CancellationToken ct)
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
                var seasonLabel = string.IsNullOrEmpty(seasonInfo.Label) ? $"#{seasonId}" : seasonInfo.Label;
                var nonDisputata = nonDisputataSeasonLabel is not null &&
                                    string.Equals(seasonLabel, nonDisputataSeasonLabel, StringComparison.OrdinalIgnoreCase);

                return new DualSourceResultRowViewModel
                {
                    SeasonLabel = seasonLabel,
                    SeasonOrder = seasonInfo.Order,
                    NonDisputata = nonDisputata,
                    SourceA = nonDisputata ? null : teams.GetValueOrDefault(r.GetValueOrDefault(sourceAColumn) as int? ?? 0),
                    SourceB = nonDisputata ? null : teams.GetValueOrDefault(r.GetValueOrDefault(sourceBColumn) as int? ?? 0),
                    Vincitore = nonDisputata ? null : teams.GetValueOrDefault(r.GetValueOrDefault("Vincitore") as int? ?? 0),
                    Risultato = nonDisputata ? null : r.GetValueOrDefault("Risultato") as string,
                    SedeFinale = nonDisputata ? null : teams.GetValueOrDefault(r.GetValueOrDefault("SedeFinale") as int? ?? 0),
                    SedeFinaleStadio = nonDisputata ? null : r.GetValueOrDefault("SedeFinaleStadio") as string
                };
            })
            .OrderBy(r => r.SeasonOrder)
            .ToList();
    }

    /// <summary>Risultati Popa Libertadores (FFM.PopaLibertadoresStatistiche): unica competizione a doppio turno.</summary>
    public async Task<IReadOnlyList<PopaLibertadoresResultRowViewModel>> GetPopaLibertadoresResultsAsync(CancellationToken ct)
    {
        var entity = await _content.GetEntityAsync("FFM", "PopaLibertadoresStatistiche", ct);
        if (entity is null)
        {
            _logger.LogWarning("FFM.PopaLibertadoresStatistiche non risulta ancora scaffoldata: la sezione viene omessa.");
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
                return new PopaLibertadoresResultRowViewModel
                {
                    SeasonLabel = string.IsNullOrEmpty(seasonInfo.Label) ? $"#{seasonId}" : seasonInfo.Label,
                    SeasonOrder = seasonInfo.Order,
                    Vincitore = teams.GetValueOrDefault(r.GetValueOrDefault("Vincitore") as int? ?? 0),
                    FinalistaPerdente = teams.GetValueOrDefault(r.GetValueOrDefault("FinalistaPerdente") as int? ?? 0),
                    RisultatoAndata = r.GetValueOrDefault("RisultatoAndata") as string,
                    SedeAndata = teams.GetValueOrDefault(r.GetValueOrDefault("SedeFinaleAndata") as int? ?? 0),
                    SedeAndataStadio = r.GetValueOrDefault("SedeFinaleAndataStadio") as string,
                    RisultatoRitorno = r.GetValueOrDefault("RisultatoRitorno") as string,
                    SedeRitorno = teams.GetValueOrDefault(r.GetValueOrDefault("SedeFinaleRitorno") as int? ?? 0),
                    SedeRitornoStadio = r.GetValueOrDefault("SedeFinaleRitornoStadio") as string
                };
            })
            .OrderBy(r => r.SeasonOrder)
            .ToList();
    }

    // Colonne fisse per squadra (Allenatori/Presidenti) — scelta esplicita di Alessio,
    // replica esatta della logica legacy (switch per id squadra), non generata dai dati
    // come invece l'Albo d'oro della Homepage. Stessi id/abbreviazioni di alb_tabella_
    // allenatori.js / alb_tabella_presidenti.js del client legacy.
    private static readonly (string Abbr, int TeamId)[] AllenatoriColumns =
    [
        ("VBA", 2), ("VCA", 3), ("GAR", 4), ("PZT", 5), ("OCL", 8), ("RDF", 9),
        ("BPG", 10), ("ADF", 11), ("RAT", 12), ("KKL", 13), ("PES", 14), ("SAL", 15), ("NRK", 16)
    ];

    private static readonly (string Abbr, int TeamId)[] PresidentiColumns =
    [
        .. AllenatoriColumns,
        ("SPI", 18), ("AQS", 20), ("MAU", 21)
    ];

    private static readonly string[] CampionatoPositionColumns =
        ["Primo", "Secondo", "Terzo", "Quarto", "Quinto", "Sesto", "Settimo", "Ottavo", "Nono", "Decimo", "Undicesimo", "Dodicesimo"];

    /// <summary>Pivot Allenatori: una riga per stagione, una colonna per squadra (fisse), cella = nome/i allenatore/i (WN_LOOKUP, lista separata da virgole).</summary>
    public async Task<PivotTableViewModel?> GetAllenatoriPivotAsync(CancellationToken ct)
    {
        var entity = await _content.GetEntityAsync("FFM", "AllenatoriStatistiche", ct);
        if (entity is null)
        {
            _logger.LogWarning("FFM.AllenatoriStatistiche non risulta ancora scaffoldata: la sezione viene omessa.");
            return null;
        }

        var lookups = await GetLookupsAsync(ct);
        var rows = await _content.GetAllRowsAsync(entity, ct: ct);

        string ResolveAllenatori(IReadOnlyDictionary<string, object?> row)
        {
            var raw = row.GetValueOrDefault("Allenatori") as string;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "N/A";
            }

            var names = raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out var id) ? lookups.GetValueOrDefault(id).Label : null)
                .Where(n => !string.IsNullOrEmpty(n));

            var joined = string.Join(" / ", names);
            return string.IsNullOrEmpty(joined) ? "N/A" : joined;
        }

        return BuildPivot(rows, lookups, AllenatoriColumns, ResolveAllenatori, extraColumnHeader: "Giornate",
            extraLabelSelector: row => row.GetValueOrDefault("Giornate") as string);
    }

    /// <summary>Pivot Presidenti: una riga per stagione, una colonna per squadra (fisse), cella = nome presidente (testo semplice).</summary>
    public async Task<PivotTableViewModel?> GetPresidentiPivotAsync(CancellationToken ct)
    {
        var entity = await _content.GetEntityAsync("FFM", "PresidentiStatistiche", ct);
        if (entity is null)
        {
            _logger.LogWarning("FFM.PresidentiStatistiche non risulta ancora scaffoldata: la sezione viene omessa.");
            return null;
        }

        var lookups = await GetLookupsAsync(ct);
        var rows = await _content.GetAllRowsAsync(entity, ct: ct);

        return BuildPivot(rows, lookups, PresidentiColumns,
            row => (row.GetValueOrDefault("Presidente") as string) is { Length: > 0 } presidente ? presidente : "N/A",
            extraColumnHeader: null, extraLabelSelector: null);
    }

    private static PivotTableViewModel? BuildPivot(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        IReadOnlyDictionary<int, (string Label, int Order)> lookups,
        (string Abbr, int TeamId)[] columns,
        Func<IReadOnlyDictionary<string, object?>, string> cellSelector,
        string? extraColumnHeader,
        Func<IReadOnlyDictionary<string, object?>, string?>? extraLabelSelector)
    {
        var seasons = rows
            .Select(r => r.GetValueOrDefault("Stagione") as int? ?? 0)
            .Distinct()
            .Select(id => (Id: id, Info: lookups.GetValueOrDefault(id)))
            .OrderBy(x => x.Info.Order)
            .ToList();

        var pivotRows = seasons
            .Select(season =>
            {
                var seasonRows = rows.Where(r => (r.GetValueOrDefault("Stagione") as int? ?? 0) == season.Id).ToList();

                var cells = columns
                    .Select(col => seasonRows.FirstOrDefault(r => (r.GetValueOrDefault("Squadra") as int? ?? 0) == col.TeamId))
                    .Select(row => row is null ? "N/A" : cellSelector(row))
                    .ToList();

                return new PivotRowViewModel
                {
                    SeasonLabel = string.IsNullOrEmpty(season.Info.Label) ? $"#{season.Id}" : season.Info.Label,
                    SeasonOrder = season.Info.Order,
                    ExtraLabel = extraLabelSelector is null ? null : seasonRows.Select(extraLabelSelector).FirstOrDefault(l => !string.IsNullOrEmpty(l)),
                    Cells = cells
                };
            })
            .ToList();

        return pivotRows.Count == 0
            ? null
            : new PivotTableViewModel
            {
                ColumnHeaders = columns.Select(c => c.Abbr).ToList(),
                ExtraColumnHeader = extraColumnHeader,
                Rows = pivotRows
            };
    }

    /// <summary>
    /// Partecipazioni Campionato: un aggregato per squadra (titoli/2°/3°/podi/partecipazioni),
    /// costruito dinamicamente dai dati (Primo..Dodicesimo di FFM.CampionatoStatistiche) —
    /// già così nel client legacy, nessun id hardcoded qui (a differenza dei pivot sopra).
    /// </summary>
    public async Task<IReadOnlyList<PartecipazioniRowViewModel>> GetCampionatoPartecipazioniAsync(CancellationToken ct)
    {
        var entity = await _content.GetEntityAsync("FFM", "CampionatoStatistiche", ct);
        if (entity is null)
        {
            _logger.LogWarning("FFM.CampionatoStatistiche non risulta ancora scaffoldata: la sezione partecipazioni viene omessa.");
            return [];
        }

        var teams = await GetTeamsAsync(ct);
        var rows = await _content.GetAllRowsAsync(entity, ct: ct);

        var stats = new Dictionary<int, (int Titoli, int Secondo, int Terzo, int Partecipazioni)>();

        foreach (var row in rows)
        {
            var seenInThisRow = new HashSet<int>();
            for (var position = 0; position < CampionatoPositionColumns.Length; position++)
            {
                var teamId = row.GetValueOrDefault(CampionatoPositionColumns[position]) as int? ?? 0;
                if (teamId <= 0 || !seenInThisRow.Add(teamId))
                {
                    continue;
                }

                var current = stats.GetValueOrDefault(teamId);
                stats[teamId] = (
                    current.Titoli + (position == 0 ? 1 : 0),
                    current.Secondo + (position == 1 ? 1 : 0),
                    current.Terzo + (position == 2 ? 1 : 0),
                    current.Partecipazioni + 1);
            }
        }

        return stats
            .Select(kv => new PartecipazioniRowViewModel
            {
                Team = teams.GetValueOrDefault(kv.Key),
                Titoli = kv.Value.Titoli,
                SecondoPosto = kv.Value.Secondo,
                TerzoPosto = kv.Value.Terzo,
                Podi = kv.Value.Titoli + kv.Value.Secondo + kv.Value.Terzo,
                Partecipazioni = kv.Value.Partecipazioni
            })
            .OrderByDescending(r => r.Titoli)
            .ThenByDescending(r => r.Podi)
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
