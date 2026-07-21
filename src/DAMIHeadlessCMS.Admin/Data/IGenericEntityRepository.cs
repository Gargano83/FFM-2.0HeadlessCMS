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
    /// <param name="resolveForeignKeys">
    /// True per risolvere anche i campi FK nella loro etichetta (uso tipico: griglia
    /// dati del backoffice, sola lettura per l'utente). Default false: i consumatori
    /// esterni (host) che leggono via questo metodo si aspettano il valore grezzo,
    /// per poterlo eventualmente risolvere/join-are a modo loro (es. TestHost).
    /// </param>
    Task<GenericEntityPage> GetListAsync(
        EntityDefinition entity, int page, int pageSize, bool resolveForeignKeys = false, CancellationToken ct = default);

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
    /// filtrate da searchText (LIKE case-insensitive sulla colonna display) e,
    /// opzionalmente, da condizioni fisse aggiuntive (vedi
    /// <see cref="FieldDefinition.ForeignKeyFiltersJson"/>) — utile quando la stessa
    /// tabella di destinazione serve più campi/liste distinte (es. WN_LOOKUP).
    /// </summary>
    Task<IReadOnlyList<LookupOption>> GetLookupOptionsAsync(
        EntityDefinition targetEntity,
        string? displayColumn,
        string? searchText,
        IReadOnlyList<ForeignKeyFilterCondition>? filters = null,
        CancellationToken ct = default);

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

/// <summary>
/// Condizione di filtro fissa per le opzioni di una FK (vedi
/// <see cref="FieldDefinition.ForeignKeyFiltersJson"/> e
/// <see cref="IGenericEntityRepository.GetLookupOptionsAsync"/>). Stessa forma di
/// <see cref="QueryFilter"/> ma con Value sempre stringa (così come arriva dal wizard/JSON),
/// convertito al tipo reale della colonna di destinazione al momento della query.
/// </summary>
public sealed record ForeignKeyFilterCondition(string ColumnName, QueryFilterOperator Operator, string Value);