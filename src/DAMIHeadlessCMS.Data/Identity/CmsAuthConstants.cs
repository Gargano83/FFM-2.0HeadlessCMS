namespace DAMIHeadlessCMS.Data.Identity;

/// <summary>Nomi delle policy di autorizzazione usate nel backoffice DAMIHeadlessCMS.</summary>
public static class CmsAuthConstants
{
    /// <summary>Solo CmsAdmin: struttura, scaffolding, gestione utenti.</summary>
    public const string AdminPolicy = "DAMIHeadlessCMS.RequireAdmin";

    /// <summary>CmsAdmin o CmsEditor: CRUD sui dati.</summary>
    public const string EditorPolicy = "DAMIHeadlessCMS.RequireEditor";
}