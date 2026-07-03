namespace MyCms.Data.Identity;

/// <summary>Ruoli del backoffice MyCms.</summary>
public static class CmsRoles
{
    /// <summary>Accesso completo: dati, struttura, scaffolding, utenti.</summary>
    public const string Admin = "CmsAdmin";

    /// <summary>Solo CRUD sui dati delle entità già scaffoldate.</summary>
    public const string Editor = "CmsEditor";

    public static readonly string[] All = { Admin, Editor };
}