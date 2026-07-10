using Microsoft.Data.SqlClient;

namespace DAMIHeadlessCMS.Admin.Ffm.Data;

public class FfmUserResolver : IFfmUserResolver
{
    private readonly string _connectionString;

    public FfmUserResolver(string connectionString)
    {
        _connectionString = connectionString;
    }

    private const string Sql = "SELECT TOP (1) UT_ID FROM dbo.WN_UTENTI WHERE UT_Email = @Email;";

    public async Task<int?> ResolveIdUtenteAsync(string? email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(Sql, connection);
        command.Parameters.AddWithValue("@Email", email);

        var result = await command.ExecuteScalarAsync(ct);
        return result is null or DBNull ? null : Convert.ToInt32(result);
    }
}
