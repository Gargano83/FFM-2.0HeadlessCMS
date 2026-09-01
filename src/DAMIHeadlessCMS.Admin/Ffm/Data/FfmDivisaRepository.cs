using Microsoft.Data.SqlClient;
using DAMIHeadlessCMS.Admin.Ffm.Models;

namespace DAMIHeadlessCMS.Admin.Ffm.Data;

public class FfmDivisaRepository : IFfmDivisaRepository
{
    // Colori neutri usati come default quando una squadra non ha ancora
    // personalizzato nulla — scelta arbitraria (bianco/nero/nero), non ha
    // alcun significato oltre a "qualcosa di ragionevole da mostrare finché
    // l'utente non sceglie i propri colori".
    private const string ColoreDefault1 = "#FFFFFF";
    private const string ColoreDefault2 = "#000000";
    private const string ColoreDefault3 = "#000000";

    private readonly string _connectionString;

    public FfmDivisaRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private const string DivisaEsistenteSql = """
        SELECT SM.IdSquadra, SM.IdTemplate, DT.CartellaAsset,
               SM.Colore1, SM.Colore2, SM.Colore3,
               SM.TestoSponsor, SM.ColoreTestoSponsor, SM.ColoreContornoTestoSponsor, SM.ColoreSfondoTestoSponsor,
               SM.PosizioneTestoSponsor, SM.FontTestoSponsor, SM.ColoreOmbraTestoSponsor,
               SM.DimensioneTestoSponsor, SM.AutoFitTestoSponsor, SM.LetteringAdArcoTestoSponsor,
               SM.UrlImmagineGenerata, SM.DataUltimaModifica
        FROM FFM.SquadreMaglia SM
        JOIN FFM.DivisaTemplate DT ON DT.Id = SM.IdTemplate
        WHERE SM.IdSquadra = @IdSquadra;
        """;

    // Primo template attivo per Ordine — usato solo per costruire il default
    // quando la squadra non ha ancora una riga in FFM.SquadreMaglia.
    private const string PrimoTemplateAttivoSql = """
        SELECT TOP (1) Id, CartellaAsset
        FROM FFM.DivisaTemplate
        WHERE Attivo = 1
        ORDER BY Ordine, Id;
        """;

    public async Task<DivisaSquadraDto?> GetDivisaAsync(int idSquadra, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using (var command = new SqlCommand(DivisaEsistenteSql, connection))
        {
            command.Parameters.AddWithValue("@IdSquadra", idSquadra);

            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new DivisaSquadraDto
                {
                    IdSquadra = reader.GetInt32(reader.GetOrdinal("IdSquadra")),
                    IdTemplate = reader.GetInt32(reader.GetOrdinal("IdTemplate")),
                    CartellaAssetTemplate = reader["CartellaAsset"] as string ?? string.Empty,
                    Colore1 = reader["Colore1"] as string ?? ColoreDefault1,
                    Colore2 = reader["Colore2"] as string ?? ColoreDefault2,
                    Colore3 = reader["Colore3"] as string ?? ColoreDefault3,
                    TestoSponsor = reader["TestoSponsor"] as string,
                    ColoreTestoSponsor = reader["ColoreTestoSponsor"] as string,
                    ColoreContornoTestoSponsor = reader["ColoreContornoTestoSponsor"] as string,
                    ColoreSfondoTestoSponsor = reader["ColoreSfondoTestoSponsor"] as string,
                    PosizioneTestoSponsor = reader["PosizioneTestoSponsor"] as string ?? "Alto",
                    FontTestoSponsor = reader["FontTestoSponsor"] as string ?? "Predefinito",
                    ColoreOmbraTestoSponsor = reader["ColoreOmbraTestoSponsor"] as string,
                    DimensioneTestoSponsor = reader["DimensioneTestoSponsor"] as int?,
                    AutoFitTestoSponsor = (bool)reader["AutoFitTestoSponsor"],
                    LetteringAdArcoTestoSponsor = (bool)reader["LetteringAdArcoTestoSponsor"],
                    UrlImmagineGenerata = reader["UrlImmagineGenerata"] as string,
                    DataUltimaModifica = reader["DataUltimaModifica"] as DateTime?,
                    NonAncoraPersonalizzata = false
                };
            }
        }

        // Nessuna riga ancora salvata: costruiamo un default sensato invece di
        // restituire null, così la UI ha sempre qualcosa da mostrare.
        await using (var command = new SqlCommand(PrimoTemplateAttivoSql, connection))
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                // Caso limite: il catalogo non ha nessun template attivo, non
                // c'è nessun default costruibile.
                return null;
            }

            return new DivisaSquadraDto
            {
                IdSquadra = idSquadra,
                IdTemplate = reader.GetInt32(reader.GetOrdinal("Id")),
                CartellaAssetTemplate = reader["CartellaAsset"] as string ?? string.Empty,
                Colore1 = ColoreDefault1,
                Colore2 = ColoreDefault2,
                Colore3 = ColoreDefault3,
                TestoSponsor = null,
                ColoreTestoSponsor = null,
                ColoreContornoTestoSponsor = null,
                ColoreSfondoTestoSponsor = null,
                PosizioneTestoSponsor = "Alto",
                FontTestoSponsor = "Predefinito",
                ColoreOmbraTestoSponsor = null,
                DimensioneTestoSponsor = null,
                // Auto-fit consigliato attivo di default solo per le squadre che
                // non hanno ancora personalizzato nulla (nessuna riga esistente
                // da preservare) — vedi piano-divisa-squadra.md, sezione 8.
                AutoFitTestoSponsor = true,
                LetteringAdArcoTestoSponsor = false,
                UrlImmagineGenerata = null,
                DataUltimaModifica = null,
                NonAncoraPersonalizzata = true
            };
        }
    }

    // Upsert esplicito (IF EXISTS / UPDATE altrimenti INSERT) invece di MERGE,
    // per coerenza con lo stile del resto del modulo FFM (nessun MERGE usato
    // altrove in questo repository pattern).
    private const string UpsertDivisaSql = """
        IF EXISTS (SELECT 1 FROM FFM.SquadreMaglia WHERE IdSquadra = @IdSquadra)
        BEGIN
            UPDATE FFM.SquadreMaglia
            SET IdTemplate = @IdTemplate,
                Colore1 = @Colore1,
                Colore2 = @Colore2,
                Colore3 = @Colore3,
                TestoSponsor = @TestoSponsor,
                ColoreTestoSponsor = @ColoreTestoSponsor,
                ColoreContornoTestoSponsor = @ColoreContornoTestoSponsor,
                ColoreSfondoTestoSponsor = @ColoreSfondoTestoSponsor,
                PosizioneTestoSponsor = @PosizioneTestoSponsor,
                FontTestoSponsor = @FontTestoSponsor,
                ColoreOmbraTestoSponsor = @ColoreOmbraTestoSponsor,
                DimensioneTestoSponsor = @DimensioneTestoSponsor,
                AutoFitTestoSponsor = @AutoFitTestoSponsor,
                LetteringAdArcoTestoSponsor = @LetteringAdArcoTestoSponsor,
                UrlImmagineGenerata = @UrlImmagineGenerata,
                DataUltimaModifica = SYSUTCDATETIME(),
                IdUtenteUltimaModifica = @IdUtenteUltimaModifica
            WHERE IdSquadra = @IdSquadra;
        END
        ELSE
        BEGIN
            INSERT INTO FFM.SquadreMaglia
                (IdSquadra, IdTemplate, Colore1, Colore2, Colore3, TestoSponsor, ColoreTestoSponsor, ColoreContornoTestoSponsor, ColoreSfondoTestoSponsor,
                 PosizioneTestoSponsor, FontTestoSponsor, ColoreOmbraTestoSponsor, DimensioneTestoSponsor, AutoFitTestoSponsor, LetteringAdArcoTestoSponsor,
                 UrlImmagineGenerata, DataUltimaModifica, IdUtenteUltimaModifica)
            VALUES
                (@IdSquadra, @IdTemplate, @Colore1, @Colore2, @Colore3, @TestoSponsor, @ColoreTestoSponsor, @ColoreContornoTestoSponsor, @ColoreSfondoTestoSponsor,
                 @PosizioneTestoSponsor, @FontTestoSponsor, @ColoreOmbraTestoSponsor, @DimensioneTestoSponsor, @AutoFitTestoSponsor, @LetteringAdArcoTestoSponsor,
                 @UrlImmagineGenerata, SYSUTCDATETIME(), @IdUtenteUltimaModifica);
        END
        """;

    public async Task AggiornaDivisaAsync(int idSquadra, AggiornaDivisaRequestDto dto, int? idUtente, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(UpsertDivisaSql, connection);
        command.Parameters.AddWithValue("@IdSquadra", idSquadra);
        command.Parameters.AddWithValue("@IdTemplate", dto.IdTemplate);
        command.Parameters.AddWithValue("@Colore1", dto.Colore1);
        command.Parameters.AddWithValue("@Colore2", dto.Colore2);
        command.Parameters.AddWithValue("@Colore3", dto.Colore3);
        command.Parameters.AddWithValue("@TestoSponsor", (object?)dto.TestoSponsor ?? DBNull.Value);
        command.Parameters.AddWithValue("@ColoreTestoSponsor", (object?)dto.ColoreTestoSponsor ?? DBNull.Value);
        command.Parameters.AddWithValue("@ColoreContornoTestoSponsor", (object?)dto.ColoreContornoTestoSponsor ?? DBNull.Value);
        command.Parameters.AddWithValue("@ColoreSfondoTestoSponsor", (object?)dto.ColoreSfondoTestoSponsor ?? DBNull.Value);
        command.Parameters.AddWithValue("@PosizioneTestoSponsor", dto.PosizioneTestoSponsor);
        command.Parameters.AddWithValue("@FontTestoSponsor", dto.FontTestoSponsor);
        command.Parameters.AddWithValue("@ColoreOmbraTestoSponsor", (object?)dto.ColoreOmbraTestoSponsor ?? DBNull.Value);
        command.Parameters.AddWithValue("@DimensioneTestoSponsor", (object?)dto.DimensioneTestoSponsor ?? DBNull.Value);
        command.Parameters.AddWithValue("@AutoFitTestoSponsor", dto.AutoFitTestoSponsor);
        command.Parameters.AddWithValue("@LetteringAdArcoTestoSponsor", dto.LetteringAdArcoTestoSponsor);
        command.Parameters.AddWithValue("@UrlImmagineGenerata", (object?)dto.UrlImmagineGenerata ?? DBNull.Value);
        command.Parameters.AddWithValue("@IdUtenteUltimaModifica", (object?)idUtente ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(ct);
    }
}
