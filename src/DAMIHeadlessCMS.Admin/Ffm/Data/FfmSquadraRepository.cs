using Microsoft.Data.SqlClient;
using DAMIHeadlessCMS.Admin.Ffm.Models;

namespace DAMIHeadlessCMS.Admin.Ffm.Data;

public class FfmSquadraRepository : IFfmSquadraRepository
{
    private readonly string _connectionString;

    /// <summary>
    /// Id lingua usato per risolvere FFM.Squadre.Nome tramite la funzione
    /// legacy dbo.udf_Localize(Nome, @lg, @lgDef, ''), riusata così com'è
    /// (nessuna reimplementazione della logica di localizzazione). Finché il
    /// backoffice non ha un selettore multi-lingua, @lg e @lgDef coincidono
    /// con la lingua di default configurata per il modulo FFM.
    /// </summary>
    private readonly int _defaultLanguageId;

    public FfmSquadraRepository(string connectionString, int defaultLanguageId)
    {
        _connectionString = connectionString;
        _defaultLanguageId = defaultLanguageId;
    }

    private const string SquadreListSql = """
        SELECT Id,
               dbo.udf_Localize(Nome, @Lg, @LgDef, '') AS Nome,
               Presidente,
               Allenatore
        FROM FFM.Squadre
        ORDER BY Nome;
        """;

    public async Task<IReadOnlyList<SquadraListItemDto>> GetSquadreListAsync(CancellationToken ct = default)
    {
        var results = new List<SquadraListItemDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(SquadreListSql, connection);
        AddLanguageParameters(command);

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SquadraListItemDto
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Nome = reader["Nome"] as string ?? string.Empty,
                Presidente = reader["Presidente"] as string,
                Allenatore = reader["Allenatore"] as string
            });
        }

        return results;
    }

    // Stessa aggregazione della query legacy GetInfoSquadraById: conteggi
    // Tesserati/InPrestito/InRosa/APrestito/ListaA/Under22InRosa filtrati
    // sulla stagione attiva, più il calcolo "over 22 portieri" per ListaA.
    private const string InfoSquadraSql = """
        DECLARE @AnnoInizioStagioneAttiva INT = (
            SELECT TOP (1) AnnoInizioStagioneAttiva FROM FFM.Lega WHERE Attiva = 1
        );
        SET @AnnoInizioStagioneAttiva = ISNULL(@AnnoInizioStagioneAttiva, YEAR(GETDATE()));

        SELECT Id AS IdSquadra,
               dbo.udf_Localize(Nome, @Lg, @LgDef, '') AS NomeSquadra,
               Presidente,
               VicePresidente,
               Allenatore,
               ISNULL(DurataContrattoAllenatore, 0) AS DurataContrattoAllenatore,
               ISNULL(StipendioAllenatore, 0) AS StipendioAllenatore,
               ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori
                       WHERE IdSquadra = @Id AND Stagione = (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                         AND ISNULL(Stato, '') != 'Lista A (Pr)'), 0) AS Tesserati,
               ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori
                       WHERE IdSquadra = @Id AND Stagione = (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                         AND ISNULL(Stato, '') = 'Lista A (Pr)'), 0) AS InPrestito,
               ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori
                       WHERE IdSquadra = @Id AND Stagione = (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                         AND ISNULL(Stato, '') IN ('Lista A', 'Lista A (Pr)')), 0) AS InRosa,
               ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori
                       WHERE IdSquadra = @Id AND Stagione = (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                         AND ISNULL(Stato, '') IN ('In prestito', 'No Serie A')), 0) AS APrestito,
               CASE
                   WHEN ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori SRelG JOIN FFM.Giocatori G ON G.Id = SRelG.IdGiocatore
                                WHERE SRelG.IdSquadra = @Id AND SRelG.Stagione = (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                                  AND SRelG.Stato IN ('Lista A', 'Lista A (Pr)') AND G.Ruolo = 'Portiere'
                                  AND (@AnnoInizioStagioneAttiva - YEAR(G.DataDiNascita) > 22)), 0) > 2
                   THEN
                       ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori SRelG JOIN FFM.Giocatori G ON G.Id = SRelG.IdGiocatore
                               WHERE SRelG.IdSquadra = @Id AND SRelG.Stagione = (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                                 AND SRelG.Stato IN ('Lista A', 'Lista A (Pr)') AND G.Ruolo IN ('Attaccante', 'Difensore', 'Centrocampista')), 0)
                       - ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori SRelG JOIN FFM.Giocatori G ON G.Id = SRelG.IdGiocatore
                                 WHERE SRelG.IdSquadra = @Id AND SRelG.Stagione = (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                                   AND SRelG.Stato IN ('Lista A', 'Lista A (Pr)') AND G.Ruolo IN ('Attaccante', 'Difensore', 'Centrocampista')
                                   AND (@AnnoInizioStagioneAttiva - YEAR(G.DataDiNascita) <= 22)), 0)
                       + 2
                   ELSE
                       ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori SRelG JOIN FFM.Giocatori G ON G.Id = SRelG.IdGiocatore
                               WHERE SRelG.IdSquadra = @Id AND SRelG.Stagione = (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                                 AND SRelG.Stato IN ('Lista A', 'Lista A (Pr)') AND G.Ruolo IN ('Attaccante', 'Difensore', 'Centrocampista')), 0)
                       - ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori SRelG JOIN FFM.Giocatori G ON G.Id = SRelG.IdGiocatore
                                 WHERE SRelG.IdSquadra = @Id AND SRelG.Stagione = (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                                   AND SRelG.Stato IN ('Lista A', 'Lista A (Pr)') AND G.Ruolo IN ('Attaccante', 'Difensore', 'Centrocampista')
                                   AND (@AnnoInizioStagioneAttiva - YEAR(G.DataDiNascita) <= 22)), 0)
                       + ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori SRelG JOIN FFM.Giocatori G ON G.Id = SRelG.IdGiocatore
                                 WHERE SRelG.IdSquadra = @Id AND SRelG.Stagione = (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                                   AND SRelG.Stato IN ('Lista A', 'Lista A (Pr)') AND G.Ruolo = 'Portiere'
                                   AND (@AnnoInizioStagioneAttiva - YEAR(G.DataDiNascita) > 22)), 0)
               END AS ListaA,
               ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori SRelG JOIN FFM.Giocatori G ON G.Id = SRelG.IdGiocatore
                       WHERE SRelG.IdSquadra = @Id AND Stagione = (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                         AND @AnnoInizioStagioneAttiva - YEAR(G.DataDiNascita) <= 22
                         AND SRelG.Stato IN ('Lista A', 'Lista A (Pr)')), 0) AS Under22InRosa,
               ISNULL(RimanenzaStagionePrecedente, 0) AS RimanenzaStagionePrecedente,
               ISNULL(RefillRanking, 0) AS RefillRanking,
               ISNULL(RefillValoreSocieta, 0) AS RefillValoreSocieta,
               ISNULL(RefillStadio, 0) AS RefillStadio,
               ISNULL(RefillStipendi, 0) AS RefillStipendi,
               ISNULL(MonteStipendiAndata, 0) AS MonteStipendiAndata,
               ISNULL(MonteStipendiRitorno, 0) AS MonteStipendiRitorno,
               ISNULL(BilancioMercato, 0) AS BilancioMercato,
               ISNULL(FairPlayFinanziario, 0) AS FairPlayFinanziario,
               ISNULL(AbilitaModifica, 0) AS AbilitaModifica
        FROM FFM.Squadre
        WHERE Id = @Id;
        """;

    public async Task<InfoSquadraDto?> GetInfoSquadraAsync(int idSquadra, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(InfoSquadraSql, connection);
        command.Parameters.AddWithValue("@Id", idSquadra);
        AddLanguageParameters(command);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new InfoSquadraDto
        {
            IdSquadra = reader.GetInt32(reader.GetOrdinal("IdSquadra")),
            NomeSquadra = reader["NomeSquadra"] as string ?? string.Empty,
            Presidente = reader["Presidente"] as string,
            VicePresidente = reader["VicePresidente"] as string,
            Allenatore = reader["Allenatore"] as string,
            DurataContrattoAllenatore = reader.GetInt32(reader.GetOrdinal("DurataContrattoAllenatore")),
            StipendioAllenatore = Convert.ToDecimal(reader["StipendioAllenatore"]),
            Tesserati = reader.GetInt32(reader.GetOrdinal("Tesserati")),
            InPrestito = reader.GetInt32(reader.GetOrdinal("InPrestito")),
            InRosa = reader.GetInt32(reader.GetOrdinal("InRosa")),
            APrestito = reader.GetInt32(reader.GetOrdinal("APrestito")),
            ListaA = reader.GetInt32(reader.GetOrdinal("ListaA")),
            Under22InRosa = reader.GetInt32(reader.GetOrdinal("Under22InRosa")),
            RimanenzaStagionePrecedente = Convert.ToDecimal(reader["RimanenzaStagionePrecedente"]),
            RefillRanking = Convert.ToDecimal(reader["RefillRanking"]),
            RefillValoreSocieta = Convert.ToDecimal(reader["RefillValoreSocieta"]),
            RefillStadio = Convert.ToDecimal(reader["RefillStadio"]),
            RefillStipendi = Convert.ToDecimal(reader["RefillStipendi"]),
            MonteStipendiAndata = Convert.ToDecimal(reader["MonteStipendiAndata"]),
            MonteStipendiRitorno = Convert.ToDecimal(reader["MonteStipendiRitorno"]),
            BilancioMercato = Convert.ToDecimal(reader["BilancioMercato"]),
            FairPlayFinanziario = Convert.ToDecimal(reader["FairPlayFinanziario"]),
            AbilitaModifica = Convert.ToBoolean(reader["AbilitaModifica"])
        };
    }

    private const string RosaSql = """
        SELECT G.Id, G.Nome, G.Cognome, G.DataDiNascita, G.Ruolo,
               SRG.ValoreDiMercato, SRG.Stipendio, SRG.Stato, ISNULL(SRG.Mesi, 0) AS Mesi
        FROM FFM.Giocatori G
        JOIN FFM.SquadreRelGiocatori SRG ON SRG.IdGiocatore = G.Id AND SRG.IdSquadra = @IdSquadra
        WHERE SRG.Stagione = (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
        ORDER BY CASE WHEN G.Ruolo = 'Portiere' THEN 1
                      WHEN G.Ruolo = 'Difensore' THEN 2
                      WHEN G.Ruolo = 'Centrocampista' THEN 3
                      WHEN G.Ruolo = 'Attaccante' THEN 4
                      ELSE 5 END,
                 SRG.Stipendio DESC, SRG.ValoreDiMercato DESC, G.Nome, G.Cognome;
        """;

    public async Task<IReadOnlyList<GiocatoreSquadraDto>> GetRosaAsync(int idSquadra, CancellationToken ct = default)
    {
        var annoInizioStagioneAttiva = await GetAnnoInizioStagioneAttivaAsync(ct);
        var results = new List<GiocatoreSquadraDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(RosaSql, connection);
        command.Parameters.AddWithValue("@IdSquadra", idSquadra);

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(MapGiocatoreSquadra(reader, annoInizioStagioneAttiva));
        }

        return results;
    }

    private const string DettaglioSql = """
        SELECT G.Id, G.Nome, G.Cognome, G.DataDiNascita, G.Ruolo,
               SRG.ValoreDiMercato, SRG.Stipendio, SRG.Stato, ISNULL(SRG.Mesi, 0) AS Mesi
        FROM FFM.Giocatori G
        JOIN FFM.SquadreRelGiocatori SRG ON SRG.IdGiocatore = G.Id
        WHERE SRG.IdSquadra = @IdSquadra AND G.Id = @IdGiocatore
          AND SRG.Stagione = (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1);
        """;

    public async Task<GiocatoreSquadraDto?> GetDettaglioGiocatorePerSquadraAsync(int idSquadra, int idGiocatore, CancellationToken ct = default)
    {
        var annoInizioStagioneAttiva = await GetAnnoInizioStagioneAttivaAsync(ct);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(DettaglioSql, connection);
        command.Parameters.AddWithValue("@IdSquadra", idSquadra);
        command.Parameters.AddWithValue("@IdGiocatore", idGiocatore);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapGiocatoreSquadra(reader, annoInizioStagioneAttiva) : null;
    }

    private const string GiocatoriSvincolatiSql = """
        SELECT Id, Nome, Cognome, DataDiNascita, Ruolo, ValoreDiMercato, Stipendio
        FROM FFM.Giocatori
        WHERE Id NOT IN (SELECT IdGiocatore FROM FFM.SquadreRelGiocatori)
        ORDER BY Cognome, Nome;
        """;

    public async Task<IReadOnlyList<GiocatoreSvincolatoDto>> GetGiocatoriSvincolatiAsync(CancellationToken ct = default)
    {
        var results = new List<GiocatoreSvincolatoDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(GiocatoriSvincolatiSql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new GiocatoreSvincolatoDto
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Nome = reader["Nome"] as string ?? string.Empty,
                Cognome = reader["Cognome"] as string ?? string.Empty,
                Ruolo = reader["Ruolo"] as string,
                DataDiNascita = reader["DataDiNascita"] as DateTime?,
                ValoreDiMercato = reader["ValoreDiMercato"] is DBNull ? null : Convert.ToDecimal(reader["ValoreDiMercato"]),
                Stipendio = reader["Stipendio"] is DBNull ? null : Convert.ToDecimal(reader["Stipendio"])
            });
        }

        return results;
    }

    // Stessa logica della query legacy: l'inserimento avviene solo se esiste
    // una stagione attiva in FFM.Lega, altrimenti l'operazione non ha effetto
    // (nessuna eccezione, comportamento legacy preservato).
    private const string AggiungiGiocatoreSql = """
        DECLARE @Stagione INT = (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1);
        IF @Stagione IS NOT NULL
        BEGIN
            INSERT INTO FFM.SquadreRelGiocatori (IdSquadra, IdGiocatore, ValoreDiMercato, Stipendio, Stagione, IdUtente)
            VALUES (@IdSquadra, @IdGiocatore, @ValoreDiMercato, @Stipendio, @Stagione, @IdUtente);
        END
        """;

    public async Task AggiungiGiocatorePerSquadraAsync(int idSquadra, int idGiocatore, decimal? valoreDiMercato, decimal? stipendio, int? idUtente, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(AggiungiGiocatoreSql, connection);
        command.Parameters.AddWithValue("@IdSquadra", idSquadra);
        command.Parameters.AddWithValue("@IdGiocatore", idGiocatore);
        command.Parameters.AddWithValue("@ValoreDiMercato", (object?)valoreDiMercato ?? DBNull.Value);
        command.Parameters.AddWithValue("@Stipendio", (object?)stipendio ?? DBNull.Value);
        command.Parameters.AddWithValue("@IdUtente", (object?)idUtente ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(ct);
    }

    private const string EliminaGiocatoreSql =
        "DELETE FROM FFM.SquadreRelGiocatori WHERE IdSquadra = @IdSquadra AND IdGiocatore = @IdGiocatore;";

    public async Task EliminaGiocatorePerSquadraAsync(int idSquadra, int idGiocatore, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(EliminaGiocatoreSql, connection);
        command.Parameters.AddWithValue("@IdSquadra", idSquadra);
        command.Parameters.AddWithValue("@IdGiocatore", idGiocatore);

        await command.ExecuteNonQueryAsync(ct);
    }

    private const string AggiornaDettaglioSql = """
        UPDATE FFM.SquadreRelGiocatori
        SET Mesi = @Mesi, Stato = @Stato, IdUtente = @IdUtente
        WHERE IdSquadra = @IdSquadra AND IdGiocatore = @IdGiocatore;
        """;

    public async Task AggiornaDettaglioGiocatorePerSquadraAsync(int idSquadra, int idGiocatore, int mesi, string? stato, int? idUtente, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(AggiornaDettaglioSql, connection);
        command.Parameters.AddWithValue("@IdSquadra", idSquadra);
        command.Parameters.AddWithValue("@IdGiocatore", idGiocatore);
        command.Parameters.AddWithValue("@Mesi", mesi);
        command.Parameters.AddWithValue("@Stato", (object?)stato ?? DBNull.Value);
        command.Parameters.AddWithValue("@IdUtente", (object?)idUtente ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(ct);
    }

    // --- Helpers -----------------------------------------------------

    private void AddLanguageParameters(SqlCommand command)
    {
        command.Parameters.AddWithValue("@Lg", _defaultLanguageId);
        command.Parameters.AddWithValue("@LgDef", _defaultLanguageId);
    }

    private async Task<int> GetAnnoInizioStagioneAttivaAsync(CancellationToken ct)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(
            "SELECT TOP (1) AnnoInizioStagioneAttiva FROM FFM.Lega WHERE Attiva = 1;", connection);

        var result = await command.ExecuteScalarAsync(ct);
        return result is null or DBNull ? DateTime.Now.Year : Convert.ToInt32(result);
    }

    private static GiocatoreSquadraDto MapGiocatoreSquadra(SqlDataReader reader, int annoInizioStagioneAttiva)
    {
        var dataDiNascita = reader["DataDiNascita"] as DateTime?;
        return new GiocatoreSquadraDto
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Nome = reader["Nome"] as string ?? string.Empty,
            Cognome = reader["Cognome"] as string ?? string.Empty,
            DataDiNascita = dataDiNascita,
            Ruolo = reader["Ruolo"] as string,
            ValoreDiMercato = reader["ValoreDiMercato"] is DBNull ? null : Convert.ToDecimal(reader["ValoreDiMercato"]),
            Stipendio = reader["Stipendio"] is DBNull ? null : Convert.ToDecimal(reader["Stipendio"]),
            Stato = reader["Stato"] as string,
            Mesi = Convert.ToInt32(reader["Mesi"]),
            U22 = dataDiNascita.HasValue && annoInizioStagioneAttiva - dataDiNascita.Value.Year <= 22
        };
    }
}
