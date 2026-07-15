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

    /// <summary>
    /// Prefisso di percorso interno (es. "/categorie") per l'URL di dettaglio
    /// dei singoli record di questa entità. Opzionale: se null, l'entità non
    /// ha un routing di dettaglio configurato (comportamento identico a prima
    /// di questa fase). Il CMS valida solo che il prefisso sia univoco nello
    /// spazio di URL che conosce (vedi InternalUrlPath); il routing runtime
    /// vero e proprio — far corrispondere l'URL in ingresso a questo prefisso
    /// ed estrarne il record — resta responsabilità del progetto host.
    /// </summary>
    public string? DetailRoutePrefix { get; set; }

    /// <summary>
    /// Campo che fornisce il segmento URL del singolo record (es. uno Slug
    /// dedicato). Se null ma DetailRoutePrefix è valorizzato, la convenzione
    /// è usare la chiave primaria come fallback. Ha senso solo insieme a
    /// DetailRoutePrefix: settarlo da solo non produce alcun URL.
    /// </summary>
    public Guid? DetailKeyFieldId { get; set; }

    public FieldDefinition? DetailKeyField { get; set; }

    public ICollection<FieldDefinition> Fields { get; set; } = new List<FieldDefinition>();

    /// <summary>Nome completo qualificato "schema.tabella", utile per costruire SQL.</summary>
    public string QualifiedTableName => $"{SchemaName}.{TableName}";
}
