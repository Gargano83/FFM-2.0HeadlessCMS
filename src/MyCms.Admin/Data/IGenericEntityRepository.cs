using MyCms.Core.Entities;

namespace MyCms.Admin.Data;

/// <summary>
/// Accesso dati generico via SQL parametrico, guidato dai metadati di
/// EntityDefinition/FieldDefinition. Nessun ORM: le tabelle applicative non
/// sono mappate da EF Core (sono sconosciute a compile-time), quindi si opera
/// con SQL dinamico costruito SOLO a partire da identificatori whitelisted
/// (nomi di colonna/tabella già validati e persistiti in cms.FieldDefinition).
///
/// IMPORTANTE: i nomi di tabella/colonna non sono parametrizzabili in T-SQL,
/// quindi la sicurezza contro SQL injection dipende dal fatto che provengano
/// SEMPRE da EntityDefinition/FieldDefinition (mai da input utente diretto)
/// e vengano quotati con QuoteIdentifier. I VALORI sono invece sempre passati
/// come parametri SQL veri.
/// </summary>
public interface IGenericEntityRepository
{
    Task<GenericEntityPage> GetListAsync(
        EntityDefinition entity, int page, int pageSize, CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, object?>?> GetByIdAsync(
        EntityDefinition entity, object id, CancellationToken ct = default);

    /// <summary>Inserisce una riga. Ritorna il valore della PK (generato dal DB se identity).</summary>
    Task<object> CreateAsync(
        EntityDefinition entity, IReadOnlyDictionary<string, string?> formValues, CancellationToken ct = default);

    Task UpdateAsync(
        EntityDefinition entity, object id, IReadOnlyDictionary<string, string?> formValues, CancellationToken ct = default);

    Task DeleteAsync(EntityDefinition entity, object id, CancellationToken ct = default);

    /// <summary>
    /// Righe minime {Value, Label} per popolare una &lt;select&gt; FK via AJAX.
    /// Se displayColumn è null, usa la PK anche come label. Il filtro q, se
    /// presente, fa un LIKE case-insensitive sulla colonna display.
    /// </summary>
    Task<IReadOnlyList<LookupOption>> GetLookupOptionsAsync(
        EntityDefinition targetEntity, string? displayColumn, string? searchText, CancellationToken ct = default);
}

/// <summary>Risultato paginato per la vista Index del CRUD generico.</summary>
public sealed record GenericEntityPage(
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>Coppia valore/etichetta per una &lt;select&gt; FK.</summary>
public sealed record LookupOption(string Value, string Label);
