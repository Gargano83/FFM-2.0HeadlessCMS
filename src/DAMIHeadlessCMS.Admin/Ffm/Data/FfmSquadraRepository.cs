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

    private const string UpdateLogoStatisticheSql = "UPDATE FFM.Squadre SET LogoStatistiche = @Path WHERE Id = @Id;";

    public async Task UpdateLogoStatisticheAsync(int idSquadra, string relativePath, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(UpdateLogoStatisticheSql, connection);
        command.Parameters.AddWithValue("@Id", idSquadra);
        command.Parameters.AddWithValue("@Path", relativePath);
        await command.ExecuteNonQueryAsync(ct);
    }

    // Stesso filtro della query legacy "Club_GetSquadreAttive": una squadra è
    // "attiva" se ha un utente presidente (UT_TIPOLOGIA = 4) attivo (UT_attivo = 1)
    // associato tramite UT_Squadra. A differenza di SquadreListSql, filtra
    // deliberatamente — è pensata solo per il selettore dell'Area Riservata, mai
    // per l'elenco squadre del backoffice (che deve restare completo).
    private const string SquadreAttiveSql = """
        SELECT S.Id,
               dbo.udf_Localize(S.Nome, @Lg, @LgDef, '') AS Nome,
               S.Presidente,
               S.Allenatore
        FROM FFM.Squadre S
        INNER JOIN WN_UTENTI U ON U.UT_Squadra = S.Id
        WHERE ISNULL(U.UT_TIPOLOGIA, 0) = 4
          AND ISNULL(U.UT_attivo, 0) = 1
        ORDER BY Nome;
        """;

    public async Task<IReadOnlyList<SquadraListItemDto>> GetSquadreAttiveAsync(CancellationToken ct = default)
    {
        var results = new List<SquadraListItemDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(SquadreAttiveSql, connection);
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
                       WHERE IdSquadra = @Id AND Stagione <= (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                         AND ISNULL(Stato, '') != 'Lista A (Pr)'), 0) AS Tesserati,
               ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori
                       WHERE IdSquadra = @Id AND Stagione <= (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                         AND ISNULL(Stato, '') = 'Lista A (Pr)'), 0) AS InPrestito,
               ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori
                       WHERE IdSquadra = @Id AND Stagione <= (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                         AND ISNULL(Stato, '') IN ('Lista A', 'Lista A (Pr)')), 0) AS InRosa,
               ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori
                       WHERE IdSquadra = @Id AND Stagione <= (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                         AND ISNULL(Stato, '') IN ('In prestito', 'No Serie A')), 0) AS APrestito,
               CASE
                   WHEN ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori SRelG JOIN FFM.Giocatori G ON G.Id = SRelG.IdGiocatore
                                WHERE SRelG.IdSquadra = @Id AND SRelG.Stagione <= (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                                  AND SRelG.Stato IN ('Lista A', 'Lista A (Pr)') AND G.Ruolo = 'Portiere'
                                  AND (@AnnoInizioStagioneAttiva - YEAR(G.DataDiNascita) > 22)), 0) > 2
                   THEN
                       ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori SRelG JOIN FFM.Giocatori G ON G.Id = SRelG.IdGiocatore
                               WHERE SRelG.IdSquadra = @Id AND SRelG.Stagione <= (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                                 AND SRelG.Stato IN ('Lista A', 'Lista A (Pr)') AND G.Ruolo IN ('Attaccante', 'Difensore', 'Centrocampista')), 0)
                       - ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori SRelG JOIN FFM.Giocatori G ON G.Id = SRelG.IdGiocatore
                                 WHERE SRelG.IdSquadra = @Id AND SRelG.Stagione <= (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                                   AND SRelG.Stato IN ('Lista A', 'Lista A (Pr)') AND G.Ruolo IN ('Attaccante', 'Difensore', 'Centrocampista')
                                   AND (@AnnoInizioStagioneAttiva - YEAR(G.DataDiNascita) <= 22)), 0)
                       + 2
                   ELSE
                       ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori SRelG JOIN FFM.Giocatori G ON G.Id = SRelG.IdGiocatore
                               WHERE SRelG.IdSquadra = @Id AND SRelG.Stagione <= (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                                 AND SRelG.Stato IN ('Lista A', 'Lista A (Pr)') AND G.Ruolo IN ('Attaccante', 'Difensore', 'Centrocampista')), 0)
                       - ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori SRelG JOIN FFM.Giocatori G ON G.Id = SRelG.IdGiocatore
                                 WHERE SRelG.IdSquadra = @Id AND SRelG.Stagione <= (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                                   AND SRelG.Stato IN ('Lista A', 'Lista A (Pr)') AND G.Ruolo IN ('Attaccante', 'Difensore', 'Centrocampista')
                                   AND (@AnnoInizioStagioneAttiva - YEAR(G.DataDiNascita) <= 22)), 0)
                       + ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori SRelG JOIN FFM.Giocatori G ON G.Id = SRelG.IdGiocatore
                                 WHERE SRelG.IdSquadra = @Id AND SRelG.Stagione <= (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
                                   AND SRelG.Stato IN ('Lista A', 'Lista A (Pr)') AND G.Ruolo = 'Portiere'
                                   AND (@AnnoInizioStagioneAttiva - YEAR(G.DataDiNascita) > 22)), 0)
               END AS ListaA,
               ISNULL((SELECT COUNT(*) FROM FFM.SquadreRelGiocatori SRelG JOIN FFM.Giocatori G ON G.Id = SRelG.IdGiocatore
                       WHERE SRelG.IdSquadra = @Id AND Stagione <= (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
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

    // Ordinamento aggiornato per riflettere i ruoli specifici di
    // FFM.SquadreRelGiocatori.Ruolo (vedi RuoloRosaCodes), nello stesso
    // ordine di formazione mostrato nel selettore a tag: portiere, poi linea
    // difensiva (Ds/Dc/Dd/B), centrocampo (E/M/C), trequarti (W/T), attacco
    // (A/Pc). Se un giocatore ha più ruoli specifici viene ordinato in base
    // al primo che compare in questa gerarchia (CHARINDEX si ferma al primo
    // match). Finché SRG.Ruolo non è stato valorizzato (giocatori non ancora
    // toccati dal nuovo editor) si ricade sul vecchio ordinamento per ruolo
    // base di FFM.Giocatori, mappato sulla stessa gerarchia.
    private const string RuoloOrdineCase = """
        CASE
            WHEN CHARINDEX(',P,', ISNULL(SRG.Ruolo, '')) > 0 THEN 1
            WHEN CHARINDEX(',Ds,', ISNULL(SRG.Ruolo, '')) > 0 THEN 2
            WHEN CHARINDEX(',Dc,', ISNULL(SRG.Ruolo, '')) > 0 THEN 3
            WHEN CHARINDEX(',Dd,', ISNULL(SRG.Ruolo, '')) > 0 THEN 4
            WHEN CHARINDEX(',B,', ISNULL(SRG.Ruolo, '')) > 0 THEN 5
            WHEN CHARINDEX(',E,', ISNULL(SRG.Ruolo, '')) > 0 THEN 6
            WHEN CHARINDEX(',M,', ISNULL(SRG.Ruolo, '')) > 0 THEN 7
            WHEN CHARINDEX(',C,', ISNULL(SRG.Ruolo, '')) > 0 THEN 8
            WHEN CHARINDEX(',W,', ISNULL(SRG.Ruolo, '')) > 0 THEN 9
            WHEN CHARINDEX(',T,', ISNULL(SRG.Ruolo, '')) > 0 THEN 10
            WHEN CHARINDEX(',A,', ISNULL(SRG.Ruolo, '')) > 0 THEN 11
            WHEN CHARINDEX(',Pc,', ISNULL(SRG.Ruolo, '')) > 0 THEN 12
            WHEN G.Ruolo = 'Portiere' THEN 1
            WHEN G.Ruolo = 'Difensore' THEN 3
            WHEN G.Ruolo = 'Centrocampista' THEN 8
            WHEN G.Ruolo = 'Attaccante' THEN 11
            ELSE 13
        END
        """;

    // Prezzo di acquisto più recente del giocatore per QUESTA squadra (non un
    // movimento qualsiasi in tabella: IdSquadraA è la squadra che acquisisce
    // il giocatore in quel movimento — vedi FFM.MovimentiBilancio). Solo i
    // movimenti confermati (ConfermaMovimento = 1) contano come acquisto
    // effettivo; rifiutati e in attesa vengono ignorati. Colonna prezzo
    // diversa a seconda della tipologia: PrezzoGiocatore per 285
    // (InserimentoGiocatoriDaAste), Prezzo per 287 (TrasferimentoGiocatore).
    private const string PrezzoAcquistoApply = """
        OUTER APPLY (
            SELECT TOP 1 CASE WHEN MB.TipologiaMovimento = 285 THEN MB.PrezzoGiocatore ELSE MB.Prezzo END AS Prezzo
            FROM FFM.MovimentiBilancio MB
            WHERE MB.IdGiocatore = G.Id
              AND MB.IdSquadraA = SRG.IdSquadra
              AND MB.TipologiaMovimento IN (285, 287)
              AND MB.ConfermaMovimento = 1
            ORDER BY MB.DataCreazione DESC, MB.Id DESC
        ) PA
        """;

    private static readonly string RosaSql = $"""
        SELECT G.Id, G.Nome, G.Cognome, G.DataDiNascita, G.Ruolo,
               SRG.ValoreDiMercato, SRG.Stipendio, SRG.Stato, SRG.Ruolo AS RuoloSpecifico,
               PA.Prezzo AS PrezzoAcquisto
        FROM FFM.Giocatori G
        JOIN FFM.SquadreRelGiocatori SRG ON SRG.IdGiocatore = G.Id AND SRG.IdSquadra = @IdSquadra
        {PrezzoAcquistoApply}
        WHERE SRG.Stagione <= (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1)
        ORDER BY {RuoloOrdineCase},
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

    private static readonly string DettaglioSql = $"""
        SELECT G.Id, G.Nome, G.Cognome, G.DataDiNascita, G.Ruolo,
               SRG.ValoreDiMercato, SRG.Stipendio, SRG.Stato, SRG.Ruolo AS RuoloSpecifico,
               PA.Prezzo AS PrezzoAcquisto
        FROM FFM.Giocatori G
        JOIN FFM.SquadreRelGiocatori SRG ON SRG.IdGiocatore = G.Id
        {PrezzoAcquistoApply}
        WHERE SRG.IdSquadra = @IdSquadra AND G.Id = @IdGiocatore
          AND SRG.Stagione <= (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1);
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

    private const string CercaGiocatoriSvincolatiSql = """
        SELECT TOP (@Limit) Id, Nome, Cognome, DataDiNascita, Ruolo, ValoreDiMercato, Stipendio
        FROM FFM.Giocatori
        WHERE Id NOT IN (SELECT IdGiocatore FROM FFM.SquadreRelGiocatori)
          AND (Nome LIKE @Query OR Cognome LIKE @Query OR (Nome + ' ' + Cognome) LIKE @Query)
        ORDER BY Cognome, Nome;
        """;

    public async Task<IReadOnlyList<GiocatoreSvincolatoDto>> CercaGiocatoriSvincolatiAsync(string query, int limit = 15, CancellationToken ct = default)
    {
        var results = new List<GiocatoreSvincolatoDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(CercaGiocatoriSvincolatiSql, connection);
        command.Parameters.AddWithValue("@Limit", limit);
        // Il pattern LIKE è costruito qui, non nella query: mai concatenare l'input
        // dell'utente nel testo SQL, resta comunque un parametro tipizzato.
        command.Parameters.AddWithValue("@Query", $"%{query}%");

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
        SET Stato = @Stato, Ruolo = @Ruolo, IdUtente = @IdUtente
        WHERE IdSquadra = @IdSquadra AND IdGiocatore = @IdGiocatore;
        """;

    public async Task AggiornaDettaglioGiocatorePerSquadraAsync(int idSquadra, int idGiocatore, string? stato, IReadOnlyList<string>? ruoliSpecifici, int? idUtente, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(AggiornaDettaglioSql, connection);
        command.Parameters.AddWithValue("@IdSquadra", idSquadra);
        command.Parameters.AddWithValue("@IdGiocatore", idGiocatore);
        command.Parameters.AddWithValue("@Stato", (object?)stato ?? DBNull.Value);
        command.Parameters.AddWithValue("@Ruolo", (object?)RuoloRosaCodes.Format(ruoliSpecifici) ?? DBNull.Value);
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
        var ruoloBase = reader["Ruolo"] as string;
        var ruoliSpecifici = RuoloRosaCodes.Parse(reader["RuoloSpecifico"] as string);

        if (ruoliSpecifici.Count == 0)
        {
            // SRG.Ruolo non ancora valorizzato: pre-selezione di default dedotta dal
            // ruolo base, solo per la visualizzazione — vedi RuoloRosaCodes.MappaDaRuoloBase.
            var ruoloDiDefault = RuoloRosaCodes.MappaDaRuoloBase(ruoloBase);
            if (ruoloDiDefault is not null)
            {
                ruoliSpecifici = [ruoloDiDefault];
            }
        }

        return new GiocatoreSquadraDto
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Nome = reader["Nome"] as string ?? string.Empty,
            Cognome = reader["Cognome"] as string ?? string.Empty,
            DataDiNascita = dataDiNascita,
            Ruolo = ruoloBase,
            RuoliSpecifici = ruoliSpecifici,
            ValoreDiMercato = reader["ValoreDiMercato"] is DBNull ? null : Convert.ToDecimal(reader["ValoreDiMercato"]),
            Stipendio = reader["Stipendio"] is DBNull ? null : Convert.ToDecimal(reader["Stipendio"]),
            PrezzoAcquisto = reader["PrezzoAcquisto"] is DBNull ? null : Convert.ToDecimal(reader["PrezzoAcquisto"]),
            Stato = reader["Stato"] as string,
            U22 = dataDiNascita.HasValue && annoInizioStagioneAttiva - dataDiNascita.Value.Year <= 22
        };
    }
}
