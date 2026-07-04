using DAMIHeadlessCMS.Core.Enums;

namespace DAMIHeadlessCMS.Core.Entities;

/// <summary>
/// Descrive una colonna di una tabella gestita dal CMS: tipo SQL, vincoli
/// e come deve essere renderizzata nelle view generiche di CRUD.
/// </summary>
public class FieldDefinition
{
    public Guid Id { get; set; }

    public Guid EntityDefinitionId { get; set; }
    public EntityDefinition? EntityDefinition { get; set; }

    /// <summary>Nome fisico della colonna nel database.</summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>Etichetta mostrata nella UI (es. "Prezzo unitario").</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Tipo SQL nativo così come letto dallo schema (es. "nvarchar", "int", "bit").</summary>
    public string SqlDataType { get; set; } = string.Empty;

    public int? MaxLength { get; set; }

    public bool IsNullable { get; set; }

    public bool IsPrimaryKey { get; set; }

    public bool IsIdentity { get; set; }

    public bool IsForeignKey { get; set; }

    /// <summary>Se IsForeignKey è true, entità di destinazione della FK.</summary>
    public Guid? ForeignKeyTargetEntityId { get; set; }
    public EntityDefinition? ForeignKeyTargetEntity { get; set; }

    /// <summary>Colonna da mostrare nella &lt;select&gt; per la FK (es. "Name" invece dell'Id).</summary>
    public string? ForeignKeyDisplayColumn { get; set; }

    /// <summary>Editor Razor da usare per questo campo (inferito automaticamente, sovrascrivibile).</summary>
    public EditorType EditorType { get; set; }

    /// <summary>Se true, il campo compare nella vista elenco (Index).</summary>
    public bool ShowInList { get; set; } = true;

    /// <summary>Se true, il campo compare nel form di creazione/modifica.</summary>
    public bool ShowInForm { get; set; } = true;

    public bool IsRequired { get; set; }

    public int SortOrder { get; set; }
}
