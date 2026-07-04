using DAMIHeadlessCMS.Core.Entities;

namespace DAMIHeadlessCMS.Admin.ViewModels;

/// <summary>Modello per la view Index (elenco righe di un'entità).</summary>
public sealed class GenericEntityIndexViewModel
{
    public required EntityDefinition Entity { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Modello per le view Create/Edit (form di una singola riga).</summary>
public sealed class GenericEntityFormViewModel
{
    public required EntityDefinition Entity { get; init; }

    /// <summary>Valori correnti dei campi. Null in Create se non ancora inviato.</summary>
    public IReadOnlyDictionary<string, object?>? Values { get; init; }

    /// <summary>Id della riga in edit (null in Create).</summary>
    public string? RecordId { get; init; }

    public string? ErrorMessage { get; init; }
}