using System.Security.Claims;

namespace DAMIHeadlessCMS.TestHost.PublicSite;

/// <summary>
/// Lettura dei claim del cookie PublicUser specifici dell'Area Riservata,
/// condivisa tra <see cref="Controllers.AreaRiservataController"/> e
/// <see cref="Controllers.AreaRiservataApiController"/> — mai duplicare il
/// nome del claim "IdSquadra" in più punti del codice.
/// </summary>
public static class AreaRiservataClaimsExtensions
{
    public static int? GetIdSquadra(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst("IdSquadra")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    public static int? GetIdUtente(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}
