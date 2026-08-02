namespace DAMIHeadlessCMS.TestHost.PublicSite;

/// <summary>
/// Nome dello schema di autenticazione cookie per gli utenti pubblici (WN_Utenti,
/// Area Riservata) — separato dallo schema di ASP.NET Core Identity usato dal
/// backoffice (/dami), che resta lo schema di default dell'applicazione. Ogni
/// [Authorize]/SignInAsync/SignOutAsync lato pubblico deve specificare esplicitamente
/// questo schema, altrimenti ASP.NET Core userebbe quello di default (Identity).
/// </summary>
public static class PublicAuthSchemes
{
    public const string Cookie = "PublicUser";
}
