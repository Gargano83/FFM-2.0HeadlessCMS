using Microsoft.Data.SqlClient;
using DAMIHeadlessCMS.Admin.Ffm.Models;

namespace DAMIHeadlessCMS.Admin.Ffm.Data;

public class FfmDivisaTemplateRepository : IFfmDivisaTemplateRepository
{
    private readonly string _connectionString;

    public FfmDivisaTemplateRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private const string TemplateAttiviSql = """
        SELECT Id, Nome, CartellaAsset, Ordine, Attivo
        FROM FFM.DivisaTemplate
        WHERE Attivo = 1
        ORDER BY Ordine, Id;
        """;

    public async Task<IReadOnlyList<DivisaTemplateDto>> GetTemplateAttiviAsync(CancellationToken ct = default)
    {
        var results = new List<DivisaTemplateDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(TemplateAttiviSql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(MapTemplate(reader));
        }

        return results;
    }

    private const string TemplateByIdSql = """
        SELECT Id, Nome, CartellaAsset, Ordine, Attivo
        FROM FFM.DivisaTemplate
        WHERE Id = @Id;
        """;

    public async Task<DivisaTemplateDto?> GetTemplateByIdAsync(int idTemplate, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(TemplateByIdSql, connection);
        command.Parameters.AddWithValue("@Id", idTemplate);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapTemplate(reader) : null;
    }

    private static DivisaTemplateDto MapTemplate(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(reader.GetOrdinal("Id")),
        Nome = reader["Nome"] as string ?? string.Empty,
        CartellaAsset = reader["CartellaAsset"] as string ?? string.Empty,
        Ordine = reader.GetInt32(reader.GetOrdinal("Ordine")),
        Attivo = Convert.ToBoolean(reader["Attivo"])
    };
}
