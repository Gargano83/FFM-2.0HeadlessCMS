using Microsoft.AspNetCore.Http;
using DAMIHeadlessCMS.Core.Entities;

namespace DAMIHeadlessCMS.Admin.Data;

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

    /// <summary>
    /// Inserisce una riga. Ritorna il valore della PK (generato dal DB se identity).
    /// I campi EditorType.File vengono risolti da 'files': se la colonna è
    /// varbinary/binary/image i byte vanno diretti in colonna, altrimenti il file
    /// viene salvato tramite IFileStorageProvider e si persiste il percorso.
    /// </summary>
    Task<object> CreateAsync(
        EntityDefinition entity,
        IReadOnlyDictionary<string, string?> formValues,
        IReadOnlyDictionary<string, IFormFile?> files,
        CancellationToken ct = default);

    /// <summary>
    /// Aggiorna una riga. Un campo EditorType.File senza un nuovo file in 'files'
    /// viene escluso dall'UPDATE (il valore esistente non viene toccato).
    /// </summary>
    Task UpdateAsync(
        EntityDefinition entity,
        object id,
        IReadOnlyDictionary<string, string?> formValues,
        IReadOnlyDictionary<string, IFormFile?> files,
        CancellationToken ct = default);

    Task DeleteAsync(EntityDefinition entity, object id, CancellationToken ct = default);

    /// <summary>
    /// Righe minime {Value, Label} per popolare l'autocomplete di una FK via AJAX,
    /// filtrate da searchText (LIKE case-insensitive sulla colonna display).
    /// </summary>
    Task<IReadOnlyList<LookupOption>> GetLookupOptionsAsync(
        EntityDefinition targetEntity, string? displayColumn, string? searchText, CancellationToken ct = default);

    /// <summary>Etichetta di un singolo record FK, usata per pre-popolare l'autocomplete in Edit.</summary>
    Task<string?> GetLookupLabelAsync(
        EntityDefinition targetEntity, string? displayColumn, object id, CancellationToken ct = default);

    /// <summary>
    /// Lettura filtrata/ordinata/limitata, pensata per i consumatori pubblici (host)
    /// che devono leggere un sottoinsieme di righe secondo criteri noti a compile-time
    /// (es. "ultimi N articoli attivi di una categoria"), a differenza di
    /// <see cref="GetListAsync"/> che pagina l'intera tabella per la sola PK (uso CRUD
    /// di backoffice). Filtri e ordinamento operano solo su colonne NON localizzate
    /// (per una colonna localizzata il valore fisico è una chiave intera verso la
    /// LocalizationSource, non il testo — filtrare/ordinare su quel testo richiederebbe
    /// una semantica diversa, non supportata qui): se referenziata, viene lanciata
    /// <see cref="InvalidOperationException"/>. Le colonne selezionate sono tutte quelle
    /// di <c>entity.Fields</c> (stesso criterio di <see cref="GetByIdAsync"/>), incluse
    /// quelle localizzate (risolte in lettura come sempre).
    /// </summary>
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        EntityDefinition entity,
        IReadOnlyList<QueryFilter>? filters = null,
        IReadOnlyList<QuerySort>? sort = null,
        int top = 100,
        CancellationToken ct = default);
}

/// <summary>Risultato paginato per la vista Index del CRUD generico.</summary>
public sealed record GenericEntityPage(
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>Coppia valore/etichetta per una &lt;select&gt; FK.</summary>
public sealed record LookupOption(string Value, string Label);

/// <summary>Operatore di confronto per un filtro di <see cref="IGenericEntityRepository.QueryAsync"/>.</summary>
public enum QueryFilterOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

/// <summary>
/// Condizione di filtro per <see cref="IGenericEntityRepository.QueryAsync"/>: tutti i filtri
/// passati vengono combinati in AND. ColumnName deve corrispondere a un FieldDefinition
/// esistente e NON localizzato dell'entità interrogata.
/// </summary>
public sealed record QueryFilter(string ColumnName, QueryFilterOperator Operator, object? Value);

/// <summary>Criterio di ordinamento per <see cref="IGenericEntityRepository.QueryAsync"/>, applicato nell'ordine passato.</summary>
public sealed record QuerySort(string ColumnName, bool Descending = false);