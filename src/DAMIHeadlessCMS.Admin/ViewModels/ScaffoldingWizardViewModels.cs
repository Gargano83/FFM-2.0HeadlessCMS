using DAMIHeadlessCMS.Core.Enums;

namespace DAMIHeadlessCMS.Admin.ViewModels;

// --- Step 1: elenco tabelle disponibili ---

public sealed record ScaffoldingTableItem(string SchemaName, string TableName, bool AlreadyScaffolded);

public sealed record LocalizationSourceOption(Guid Id, string DisplayName);

public sealed class ScaffoldingTableListViewModel
{
    public required IReadOnlyList<ScaffoldingTableItem> Tables { get; init; }
    public required IReadOnlyList<LocalizationSourceOption> AvailableLocalizationSources { get; init; }
}

// --- Payload inviato dal wizard in fase di salvataggio finale ---

public sealed class ScaffoldingSaveRequest
{
    public List<ScaffoldingSaveEntity> Entities { get; init; } = new();
}

public sealed class ScaffoldingSaveEntity
{
    public string SchemaName { get; init; } = string.Empty;
    public string TableName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Icon { get; init; }

    /// <summary>Percorso interno per l'URL di dettaglio dei record (es. "/categorie"), opzionale.</summary>
    public string? DetailRoutePrefix { get; init; }

    /// <summary>
    /// Nome della colonna (non l'Id di FieldDefinition: per una tabella
    /// appena selezionata i FieldDefinition reali non esistono ancora al
    /// momento della compilazione di questo payload, vengono creati da
    /// ScaffoldTablesAsync subito prima che questo campo venga risolto)
    /// da usare come chiave per l'URL di dettaglio. Se null, fallback alla
    /// chiave primaria.
    /// </summary>
    public string? DetailKeyColumnName { get; init; }

    public List<ScaffoldingSaveField> Fields { get; init; } = new();
}

public sealed class ScaffoldingSaveField
{
    public string ColumnName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public EditorType EditorType { get; init; }
    public bool ShowInList { get; init; }
    public bool ShowInForm { get; init; }
    public bool IsRequired { get; init; }
    public Guid? LocalizationSourceId { get; init; }
}