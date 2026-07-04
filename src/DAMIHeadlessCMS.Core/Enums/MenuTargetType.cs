namespace DAMIHeadlessCMS.Core.Enums;

/// <summary>
/// Tipo di destinazione di una voce di menu di navigazione.
/// </summary>
public enum MenuTargetType
{
    /// <summary>Punta a una CmsPage tramite il suo Slug.</summary>
    Page = 0,

    /// <summary>Punta alla lista di un'entità generata (EntityDefinition.TableName).</summary>
    Entity = 1,

    /// <summary>URL esterno o interno gestito dall'app host.</summary>
    ExternalUrl = 2
}
