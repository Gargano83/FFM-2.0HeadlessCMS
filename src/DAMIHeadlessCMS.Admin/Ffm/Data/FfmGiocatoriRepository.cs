using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using DAMIHeadlessCMS.Admin.Ffm.Models;

namespace DAMIHeadlessCMS.Admin.Ffm.Data;

public class FfmGiocatoriRepository : IFfmGiocatoriRepository
{
    private readonly string _connectionString;

    public FfmGiocatoriRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private const string SelectAllSql = "SELECT * FROM FFM.Giocatori ORDER BY Id;";
    private const string SelectByIdSql = "SELECT * FROM FFM.Giocatori WHERE Id = @Id;";

    private const string InsertSql = """
        INSERT INTO FFM.Giocatori (Nome, Cognome, DataDiNascita, Ruolo, ValoreDiMercato, Stipendio, DataAggiornamento, Note)
        OUTPUT INSERTED.Id
        VALUES (@Nome, @Cognome, @DataDiNascita, @Ruolo, @ValoreDiMercato, @Stipendio, @DataAggiornamento, @Note);
        """;

    private const string UpdateSql = """
        UPDATE FFM.Giocatori
        SET Nome = @Nome,
            Cognome = @Cognome,
            DataDiNascita = @DataDiNascita,
            Ruolo = @Ruolo,
            ValoreDiMercato = @ValoreDiMercato,
            Stipendio = @Stipendio,
            DataAggiornamento = @DataAggiornamento,
            Note = @Note
        WHERE Id = @Id;
        """;

    private const string DeleteSql = "DELETE FROM FFM.Giocatori WHERE Id = @Id;";

    // Stessa logica della MERGE legacy (originariamente basata su XML), riscritta
    // con OPENJSON: un unico payload JSON con l'intero elenco importato.
    // - Prima MERGE: allinea FFM.Giocatori (update se Id combacia, insert se nuovo,
    //   DELETE se un giocatore esistente non è più presente nell'elenco importato).
    // - Seconda MERGE: aggiorna ValoreDiMercato/Stipendio nelle righe attive di
    //   FFM.SquadreRelGiocatori per la stagione attiva, per i giocatori importati.
    // Se non c'è una stagione attiva in FFM.Lega, l'operazione non fa nulla
    // (comportamento legacy preservato, nessuna eccezione sollevata).
    private const string ImportSql = """
        DECLARE @Stagione INT = (SELECT TOP 1 StagioneAttiva FROM FFM.Lega WHERE Attiva = 1);

        IF @Stagione IS NOT NULL
        BEGIN
            MERGE FFM.Giocatori AS T
            USING (
                SELECT *
                FROM OPENJSON(@Payload)
                WITH (
                    Id INT,
                    Nome NVARCHAR(150),
                    Cognome NVARCHAR(150),
                    DataDiNascita DATETIME,
                    Ruolo NVARCHAR(50),
                    ValoreDiMercato FLOAT,
                    Stipendio FLOAT,
                    DataAggiornamento DATETIME,
                    Note NVARCHAR(MAX)
                )
            ) AS S
            ON (T.Id = S.Id)
            WHEN MATCHED THEN UPDATE SET
                T.Nome = S.Nome,
                T.Cognome = S.Cognome,
                T.DataDiNascita = S.DataDiNascita,
                T.Ruolo = S.Ruolo,
                T.ValoreDiMercato = S.ValoreDiMercato,
                T.Stipendio = S.Stipendio,
                T.DataAggiornamento = S.DataAggiornamento,
                T.Note = S.Note
            WHEN NOT MATCHED BY TARGET THEN INSERT
                (Nome, Cognome, DataDiNascita, Ruolo, ValoreDiMercato, Stipendio, DataAggiornamento, Note)
                VALUES (S.Nome, S.Cognome, S.DataDiNascita, S.Ruolo, S.ValoreDiMercato, S.Stipendio, S.DataAggiornamento, S.Note)
            WHEN NOT MATCHED BY SOURCE THEN DELETE;

            MERGE FFM.SquadreRelGiocatori AS T
            USING (
                SELECT Id, ValoreDiMercato, Stipendio
                FROM OPENJSON(@Payload)
                WITH (Id INT, ValoreDiMercato FLOAT, Stipendio FLOAT)
            ) AS S
            ON (T.IdGiocatore = S.Id AND T.Stagione = @Stagione AND ISNULL(T.Attivo, 0) = 1)
            WHEN MATCHED THEN UPDATE SET
                T.ValoreDiMercato = S.ValoreDiMercato,
                T.Stipendio = S.Stipendio;
        END
        """;

    public async Task<IReadOnlyList<GiocatoreDto>> GetAllAsync(CancellationToken ct = default)
    {
        var results = new List<GiocatoreDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(SelectAllSql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(MapRow(reader));
        }

        return results;
    }

    public async Task<GiocatoreDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(SelectByIdSql, connection);
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapRow(reader) : null;
    }

    public async Task<GiocatoreDto> CreateAsync(GiocatoreDto giocatore, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(InsertSql, connection);
        AddCommonParameters(command, giocatore);

        var newId = (int)(await command.ExecuteScalarAsync(ct)
            ?? throw new InvalidOperationException("INSERT non ha restituito l'Id generato per il nuovo giocatore."));

        giocatore.Id = newId;
        return giocatore;
    }

    public async Task UpdateAsync(int id, GiocatoreDto giocatore, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(UpdateSql, connection);
        command.Parameters.AddWithValue("@Id", id);
        AddCommonParameters(command, giocatore);

        var affected = await command.ExecuteNonQueryAsync(ct);
        if (affected == 0)
        {
            throw new InvalidOperationException($"Nessun giocatore trovato con Id={id}.");
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(DeleteSql, connection);
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task ImportAsync(IReadOnlyList<GiocatoreDto> giocatori, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(giocatori.Select(g => new
        {
            g.Id,
            g.Nome,
            g.Cognome,
            g.DataDiNascita,
            g.Ruolo,
            ValoreDiMercato = g.ValoreDiMercato,
            Stipendio = g.Stipendio,
            g.DataAggiornamento,
            g.Note
        }));

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(ImportSql, connection);
        command.CommandTimeout = 0;
        command.Parameters.Add(new SqlParameter("@Payload", SqlDbType.NVarChar, -1) { Value = payload });

        await command.ExecuteNonQueryAsync(ct);
    }

    private static void AddCommonParameters(SqlCommand command, GiocatoreDto giocatore)
    {
        command.Parameters.AddWithValue("@Nome", giocatore.Nome);
        command.Parameters.AddWithValue("@Cognome", giocatore.Cognome);
        command.Parameters.AddWithValue("@DataDiNascita", (object?)giocatore.DataDiNascita ?? DBNull.Value);
        command.Parameters.AddWithValue("@Ruolo", (object?)giocatore.Ruolo ?? DBNull.Value);
        command.Parameters.AddWithValue("@ValoreDiMercato", (object?)giocatore.ValoreDiMercato ?? DBNull.Value);
        command.Parameters.AddWithValue("@Stipendio", (object?)giocatore.Stipendio ?? DBNull.Value);
        command.Parameters.AddWithValue("@DataAggiornamento", (object?)giocatore.DataAggiornamento ?? DBNull.Value);
        command.Parameters.AddWithValue("@Note", (object?)giocatore.Note ?? DBNull.Value);
    }

    private static GiocatoreDto MapRow(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(reader.GetOrdinal("Id")),
        Nome = reader["Nome"] as string ?? string.Empty,
        Cognome = reader["Cognome"] as string ?? string.Empty,
        DataDiNascita = reader["DataDiNascita"] as DateTime?,
        Ruolo = reader["Ruolo"] as string,
        ValoreDiMercato = reader["ValoreDiMercato"] is DBNull ? null : Convert.ToDecimal(reader["ValoreDiMercato"]),
        Stipendio = reader["Stipendio"] is DBNull ? null : Convert.ToDecimal(reader["Stipendio"]),
        DataAggiornamento = reader["DataAggiornamento"] as DateTime?,
        Note = reader["Note"] as string
    };
}
