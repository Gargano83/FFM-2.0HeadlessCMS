namespace DAMIHeadlessCMS.Data.Identity;

/// <summary>Ruoli del backoffice DAMIHeadlessCMS.</summary>
public static class CmsRoles
{
    /// <summary>Accesso completo: dati, struttura, scaffolding, utenti.</summary>
    public const string Admin = "CmsAdmin";

    /// <summary>Solo CRUD sui dati delle entità già scaffoldate.</summary>
    public const string Editor = "CmsEditor";

    /// <summary>
    /// Ruolo intermedio tra Admin ed Editor: sola lettura su Scaffolding
    /// (struttura, non l'operazione di scaffolding vera e propria), Utenti,
    /// Localizzazioni e sulle pagine dedicate del modulo FFM (Database
    /// Giocatori, Squadre/Rosa); lettura/scrittura piena su tutto il resto
    /// (Dati, Pagine, Menu).
    /// </summary>
    public const string Operator = "CmsOperator";

    public static readonly string[] All = { Admin, Editor, Operator };
}