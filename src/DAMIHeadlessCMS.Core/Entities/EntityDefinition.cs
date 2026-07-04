namespace DAMIHeadlessCMS.Core.Entities;

/// <summary>
/// Descrive una tabella del database gestita tramite il CRUD generico del CMS.
/// Popolata dal wizard di scaffolding (fase 2 della roadmap) e/o modificabile
/// manualmente dal backoffice.
/// </summary>
public class EntityDefinition
{
    public Guid Id { get; set; }

    /// <summary>Nome fisico della tabella nel database (es. "Products").</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Schema del database in cui risiede la tabella (default "dbo").</summary>
    public string SchemaName { get; set; } = "dbo";

    /// <summary>Nome mostrato nel menu/UI del backoffice (es. "Prodotti").</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Nome della colonna che funge da chiave primaria.</summary>
    public string PrimaryKeyColumn { get; set; } = string.Empty;

    /// <summary>Se false, l'entità è nascosta dal menu del backoffice senza eliminare i metadati.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Nome icona (es. da un icon set usato nella UI) mostrata nel menu.</summary>
    public string? Icon { get; set; }

    /// <summary>Ordine di visualizzazione nel menu del backoffice.</summary>
    public int SortOrder { get; set; }

    public ICollection<FieldDefinition> Fields { get; set; } = new List<FieldDefinition>();

    /// <summary>Nome completo qualificato "schema.tabella", utile per costruire SQL.</summary>
    public string QualifiedTableName => $"{SchemaName}.{TableName}";
}
