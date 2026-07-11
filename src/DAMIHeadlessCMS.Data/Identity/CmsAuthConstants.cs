namespace DAMIHeadlessCMS.Data.Identity;

/// <summary>Nomi delle policy di autorizzazione usate nel backoffice DAMIHeadlessCMS.</summary>
public static class CmsAuthConstants
{
    /// <summary>Solo CmsAdmin: struttura, scaffolding, gestione utenti.</summary>
    public const string AdminPolicy = "DAMIHeadlessCMS.RequireAdmin";

    /// <summary>CmsAdmin, CmsEditor o CmsOperator: CRUD sui dati (Dati, Pagine, Menu).</summary>
    public const string EditorPolicy = "DAMIHeadlessCMS.RequireEditor";

    /// <summary>
    /// CmsAdmin o CmsOperator: vista di sola lettura della struttura fisica di
    /// un'entità già scaffoldata (l'operazione di scaffolding/re-scaffolding
    /// resta invece riservata a AdminPolicy).
    /// </summary>
    public const string StructureViewPolicy = "DAMIHeadlessCMS.RequireStructureView";

    /// <summary>CmsAdmin o CmsOperator: pagina Utenti in sola lettura (scrittura riservata a AdminPolicy).</summary>
    public const string UsersViewPolicy = "DAMIHeadlessCMS.RequireUsersView";

    /// <summary>CmsAdmin o CmsOperator: pagina Localizzazioni in sola lettura (scrittura riservata a AdminPolicy).</summary>
    public const string LocalizationViewPolicy = "DAMIHeadlessCMS.RequireLocalizationView";

    /// <summary>CmsAdmin o CmsOperator: pagine del modulo FFM in sola lettura (scrittura riservata a AdminPolicy).</summary>
    public const string FfmViewPolicy = "DAMIHeadlessCMS.RequireFfmView";
}