using Microsoft.AspNetCore.Identity;

namespace MyCms.Data.Identity;

/// <summary>
/// Utente del backoffice MyCms. Identity dedicato, separato da eventuali
/// utenti dell'app host: vive nello schema "cms" e serve esclusivamente
/// per autenticare l'accesso al backoffice (wizard, struttura, CRUD dati).
/// </summary>
public class CmsUser : IdentityUser<Guid>
{
    /// <summary>Nome mostrato nell'header/sidebar del backoffice.</summary>
    public string? DisplayName { get; set; }
}