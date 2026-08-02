using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace DAMIHeadlessCMS.TestHost.PublicSite;

/// <summary>Dati minimi di un utente pubblico (WN_Utenti), quelli usati per il login e i claim del cookie.</summary>
public sealed record PublicUser(int Id, string Email, string? Nome, string? Cognome, int? IdSquadra);

/// <summary>
/// Accesso diretto a <c>WN_Utenti</c> per l'autenticazione dell'Area Riservata — non passa
/// dallo scaffolding/<c>IGenericEntityRepository</c> perché la verifica password richiede di
/// chiamare <c>dbo.udf_Encrypt</c> nel confronto SQL (stessa funzione del legacy, SHA2_512),
/// cosa che il repository generico non supporta (e non dovrebbe: è specifico di questa
/// tabella). Query dedicata, parametrizzata, sola lettura.
/// </summary>
public class PublicUserRepository
{
    private readonly string _connectionString;

    public PublicUserRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' non configurata.");
    }

    /// <summary>
    /// Verifica le credenziali; null se email/password non corrispondono o l'utente non è
    /// attivo. Stessa logica del legacy: <c>UT_Password = dbo.udf_Encrypt(@Password)</c>
    /// (SHA2_512) — nessuna reimplementazione dell'hashing lato .NET, per evitare rischi di
    /// mismatch di encoding/collation tra .NET e il cast a varchar(255) fatto dalla funzione SQL.
    /// </summary>
    public async Task<PublicUser?> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (1) UT_ID, UT_Email, UT_Nome, UT_Cognome, UT_Squadra
            FROM dbo.WN_Utenti
            WHERE UT_Email = @Email
              AND UT_Password = dbo.udf_Encrypt(@Password)
              AND UT_attivo = 1;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Email", SqlDbType.NVarChar, 255) { Value = email });
        command.Parameters.Add(new SqlParameter("@Password", SqlDbType.NVarChar, 255) { Value = password });

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return ReadUser(reader);
    }

    private static PublicUser ReadUser(SqlDataReader reader) => new(
        Id: reader.GetInt32(reader.GetOrdinal("UT_ID")),
        Email: reader.GetString(reader.GetOrdinal("UT_Email")),
        Nome: reader.IsDBNull(reader.GetOrdinal("UT_Nome")) ? null : reader.GetString(reader.GetOrdinal("UT_Nome")),
        Cognome: reader.IsDBNull(reader.GetOrdinal("UT_Cognome")) ? null : reader.GetString(reader.GetOrdinal("UT_Cognome")),
        IdSquadra: reader.IsDBNull(reader.GetOrdinal("UT_Squadra")) ? null : reader.GetInt32(reader.GetOrdinal("UT_Squadra")));
}
