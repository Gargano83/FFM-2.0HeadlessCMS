using DAMIHeadlessCMS.Admin.Data;
using DAMIHeadlessCMS.Core.Entities;
using DAMIHeadlessCMS.Data;
using Microsoft.EntityFrameworkCore;

namespace DAMIHeadlessCMS.TestHost.PublicSite;

/// <summary>
/// Punto unico, lato TestHost, per leggere righe di tabelle legacy scaffoldate
/// (WN_Contenuti, FFM.Squadre, ecc.) tramite <see cref="IGenericEntityRepository"/>,
/// riusando la risoluzione automatica dei campi localizzati che il repository
/// già applica in lettura (subquery su LocalizationSource, equivalente a
/// dbo.udf_Localize). Non fa parte della libreria CMS: è codice del progetto
/// host, che qui decide come e cosa leggere per le proprie pagine pubbliche.
/// </summary>
public class LegacyContentReader
{
    private readonly CmsDbContext _db;
    private readonly IGenericEntityRepository _repository;

    public LegacyContentReader(CmsDbContext db, IGenericEntityRepository repository)
    {
        _db = db;
        _repository = repository;
    }

    /// <summary>
    /// Recupera l'<see cref="EntityDefinition"/> (con i suoi Fields) per una
    /// tabella già scaffoldata da backoffice. Restituisce null se la tabella non è
    /// (ancora) stata scaffoldata in /dami/scaffolding.
    /// </summary>
    public Task<EntityDefinition?> GetEntityAsync(string schemaName, string tableName, CancellationToken ct = default) =>
        _db.EntityDefinitions
            .Include(e => e.Fields)
            .ThenInclude(f => f.LocalizationSource)
            .FirstOrDefaultAsync(e => e.SchemaName == schemaName && e.TableName == tableName, ct);

    /// <summary>
    /// Legge una singola riga per chiave primaria, restituendola come dizionario
    /// case-insensitive sul nome colonna (comodo per Razor: <c>row["co_titolo"]</c>
    /// funziona indipendentemente dal casing usato in fase di scaffold).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, object?>?> GetRowByIdAsync(
        EntityDefinition entity, object id, CancellationToken ct = default)
    {
        var row = await _repository.GetByIdAsync(entity, id, ct);
        return row is null ? null : new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Legge fino a <paramref name="maxRows"/> righe, senza filtro (equivalente delle query
    /// legacy "senza WHERE", es. FFM.Squadre). Wrapper su GetListAsync richiedendo un'unica
    /// pagina abbastanza grande: nessuna modifica alla libreria necessaria per questo caso —
    /// diverso dal caso "filtro + ordina + TOP N" (es. ultimi articoli), che invece la richiederà.
    /// </summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetAllRowsAsync(
        EntityDefinition entity, int maxRows = 500, CancellationToken ct = default)
    {
        var page = await _repository.GetListAsync(entity, page: 1, pageSize: maxRows, resolveForeignKeys: false, ct: ct);
        return page.Rows
            .Select(row => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Lettura filtrata/ordinata/limitata (es. "ultimi N articoli attivi di un tipo").
    /// Wrapper su <see cref="IGenericEntityRepository.QueryAsync"/>.
    /// </summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetFilteredRowsAsync(
        EntityDefinition entity,
        IReadOnlyList<QueryFilter>? filters = null,
        IReadOnlyList<QuerySort>? sort = null,
        int top = 100,
        CancellationToken ct = default)
    {
        var rows = await _repository.QueryAsync(entity, filters, sort, top, ct);
        return rows
            .Select(row => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }
}
