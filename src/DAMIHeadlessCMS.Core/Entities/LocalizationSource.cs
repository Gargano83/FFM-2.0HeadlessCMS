namespace DAMIHeadlessCMS.Core.Entities;

/// <summary>
/// Descrive una tabella di traduzioni "a chiave condivisa" (pattern legacy: una
/// colonna intera in una tabella applicativa non è il valore reale, ma un id di
/// contenuto da risolvere tramite questa tabella, filtrando per lingua). Non esiste
/// una foreign key fisica verso questa tabella (integrità solo applicativa), quindi
/// l'associazione va configurata manualmente sui singoli FieldDefinition.
/// </summary>
public class LocalizationSource
{
    public Guid Id { get; set; }

    /// <summary>Nome descrittivo mostrato in UI (es. "Localizzazione legacy WN").</summary>
    public string DisplayName { get; set; } = string.Empty;

    // --- Tabella di traduzione (es. WN_LOCALIZZAZIONE) ---

    public string ContentSchemaName { get; set; } = "dbo";
    public string ContentTableName { get; set; } = string.Empty;

    /// <summary>Colonna con l'id di contenuto condiviso tra le lingue (es. LC_CONT_ID).</summary>
    public string ContentIdColumn { get; set; } = string.Empty;

    /// <summary>Colonna che identifica la lingua della riga di traduzione (es. LC_LNG_ID).</summary>
    public string LanguageIdColumn { get; set; } = string.Empty;

    /// <summary>Colonna con il testo tradotto (es. LC_TESTO).</summary>
    public string TextColumn { get; set; } = string.Empty;

    /// <summary>
    /// Chiave primaria della singola riga di traduzione, se distinta da ContentIdColumn
    /// (es. LC_ID). Nullable: alcune tabelle potrebbero usare ContentId+LanguageId come
    /// chiave composta senza un id di riga proprio.
    /// </summary>
    public string? RowIdColumn { get; set; }

    // --- Tabella lingue (es. WN_LINGUE) — pronta per un futuro selettore multi-lingua ---

    public string LanguageSchemaName { get; set; } = "dbo";
    public string LanguageTableName { get; set; } = string.Empty;
    public string LanguageIdColumnInLanguageTable { get; set; } = string.Empty;
    public string? LanguageCodeColumn { get; set; }
    public string? LanguageNameColumn { get; set; }

    /// <summary>Id lingua usato per lettura/scrittura finché non esiste un selettore multi-lingua nel backoffice.</summary>
    public int DefaultLanguageId { get; set; } = 1;
}