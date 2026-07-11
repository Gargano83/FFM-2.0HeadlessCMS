using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using DAMIHeadlessCMS.Data.Identity;

namespace DAMIHeadlessCMS.Data.Extensions;

/// <summary>
/// Registra un Identity Provider dedicato al backoffice DAMIHeadlessCMS. Da chiamare
/// insieme ad AddDAMIHeadlessCMSData(...) nel Program.cs dell'app host:
///
///   builder.Services.AddDAMIHeadlessCMSData(connectionString);
///   builder.Services.AddDAMIHeadlessCMSIdentity();
///   ...
///   app.UseAuthentication();
///   app.UseAuthorization();
///
/// NOTA: usa lo schema di autenticazione standard di ASP.NET Core Identity,
/// che diventa lo scheme di default dell'applicazione. Va bene quando
/// l'host non ha un proprio Identity per il front-end (es. DAMIHeadlessCMS.TestHost).
/// Se in futuro un host avrà un proprio Identity separato, questo metodo
/// andrà esteso con uno SignInManager/scheme dedicato per evitare collisioni.
/// </summary>
public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddDAMIHeadlessCMSIdentity(
        this IServiceCollection services,
        Action<IdentityOptions>? configureIdentity = null)
    {
        services.AddIdentity<CmsUser, CmsRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.User.RequireUniqueEmail = true;
            configureIdentity?.Invoke(options);
        })
        .AddEntityFrameworkStores<CmsDbContext>()
        .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "DAMIHeadlessCMS.Auth";
            options.LoginPath = "/dami/account/login";
            options.AccessDeniedPath = "/dami/account/denied";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(CmsAuthConstants.AdminPolicy, p => p.RequireRole(CmsRoles.Admin))
            .AddPolicy(CmsAuthConstants.EditorPolicy, p => p.RequireRole(CmsRoles.Admin, CmsRoles.Editor, CmsRoles.Operator))
            .AddPolicy(CmsAuthConstants.StructureViewPolicy, p => p.RequireRole(CmsRoles.Admin, CmsRoles.Operator))
            .AddPolicy(CmsAuthConstants.UsersViewPolicy, p => p.RequireRole(CmsRoles.Admin, CmsRoles.Operator))
            .AddPolicy(CmsAuthConstants.LocalizationViewPolicy, p => p.RequireRole(CmsRoles.Admin, CmsRoles.Operator))
            .AddPolicy(CmsAuthConstants.FfmViewPolicy, p => p.RequireRole(CmsRoles.Admin, CmsRoles.Operator));

        return services;
    }
}