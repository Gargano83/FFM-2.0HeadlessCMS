using DAMIHeadlessCMS.Admin.Data;
using DAMIHeadlessCMS.Admin.Ffm.Data;
using Microsoft.Extensions.Http;

namespace DAMIHeadlessCMS.TestHost.PublicSite;

/// <summary>Esito di una passata di <see cref="LegacyMediaMigrationService.MigrateSquadreLogosAsync"/>.</summary>
public sealed class LegacyMediaMigrationResult
{
    public int Migrati { get; set; }
    public int GiaMigrati { get; set; }
    public int SenzaLogo { get; set; }
    public List<string> Saltati { get; } = [];
    public List<string> Falliti { get; } = [];
}

/// <summary>
/// Migrazione one-off dei loghi squadra (FFM.Squadre.LogoStatistiche) dal sito
/// legacy (<c>PublicSite:LegacyFileBaseUrl</c>) allo storage locale di
/// quest'host, tramite <see cref="IFileStorageProvider"/> — stesso meccanismo
/// già usato per i campi EditorType.File del CRUD generico, qui applicato a un
/// campo scaffolded (FFM.Squadre) scrivendo però solo la singola colonna
/// interessata (vedi <see cref="IFfmSquadraRepository.UpdateLogoStatisticheAsync"/>),
/// non l'intera riga.
///
/// Da lanciare una tantum, da backoffice (vedi LegacyMediaToolsController),
/// finché <c>LogoStatistiche</c> non punta più, per nessuna squadra, a un
/// percorso relativo al sito legacy — a quel punto <c>PublicSite:LegacyFileBaseUrl</c>
/// può essere rimossa dalla configurazione. Idempotente: rilanciarla su righe già
/// migrate (percorso già "uploads/...") le salta senza riscaricarle.
/// </summary>
public class LegacyMediaMigrationService
{
    private readonly LegacyContentReader _content;
    private readonly IFfmSquadraRepository _squadre;
    private readonly IFileStorageProvider _fileStorage;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LegacyMediaMigrationService> _logger;

    public LegacyMediaMigrationService(
        LegacyContentReader content,
        IFfmSquadraRepository squadre,
        IFileStorageProvider fileStorage,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<LegacyMediaMigrationService> logger)
    {
        _content = content;
        _squadre = squadre;
        _fileStorage = fileStorage;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<LegacyMediaMigrationResult> MigrateSquadreLogosAsync(CancellationToken ct = default)
    {
        var result = new LegacyMediaMigrationResult();
        var baseUrl = _configuration["PublicSite:LegacyFileBaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            result.Falliti.Add("PublicSite:LegacyFileBaseUrl non configurato: nulla da migrare.");
            return result;
        }

        var entity = await _content.GetEntityAsync("FFM", "Squadre", ct);
        if (entity is null)
        {
            result.Falliti.Add("FFM.Squadre non risulta ancora scaffoldata.");
            return result;
        }

        var rows = await _content.GetAllRowsAsync(entity, ct: ct);
        var httpClient = _httpClientFactory.CreateClient(nameof(LegacyMediaMigrationService));

        foreach (var row in rows)
        {
            if (row.GetValueOrDefault("Id") is not int idSquadra)
            {
                continue;
            }

            var logoPath = row.GetValueOrDefault("LogoStatistiche") as string;
            if (string.IsNullOrWhiteSpace(logoPath))
            {
                result.SenzaLogo++;
                continue;
            }

            if (logoPath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase) ||
                logoPath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            {
                // Già migrato in una passata precedente: idempotente, non riscaricare.
                result.GiaMigrati++;
                continue;
            }

            if (logoPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                logoPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Url assoluto ma non relativo alla base legacy configurata: non scarichiamo
                // da fonti esterne arbitrarie non previste, va gestito manualmente.
                result.Saltati.Add($"Squadra {idSquadra}: percorso già un URL assoluto non riconosciuto ({logoPath}).");
                continue;
            }

            var sourceUrl = $"{baseUrl.TrimEnd('/')}/{logoPath.TrimStart('/')}";

            try
            {
                using var response = await httpClient.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    result.Falliti.Add($"Squadra {idSquadra}: HTTP {(int)response.StatusCode} su {sourceUrl}");
                    continue;
                }

                await using var sourceStream = await response.Content.ReadAsStreamAsync(ct);
                var fileName = Path.GetFileName(logoPath);
                var newPath = await _fileStorage.SaveAsync(sourceStream, fileName, "squadre-loghi", ct);

                await _squadre.UpdateLogoStatisticheAsync(idSquadra, newPath, ct);
                result.Migrati++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Migrazione logo squadra {IdSquadra} fallita ({Url})", idSquadra, sourceUrl);
                result.Falliti.Add($"Squadra {idSquadra}: {ex.Message}");
            }
        }

        return result;
    }
}
