using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MyCms.Data.Identity;

namespace MyCms.Data.Extensions;

/// <summary>
/// Registra un Identity Provider dedicato al backoffice MyCms. Da chiamare
/// insieme ad AddMyCmsData(...) nel Program.cs dell'app host:
///
///   builder.Services.AddMyCmsData(connectionString);
///   builder.Services.AddMyCmsIdentity();
///   ...
///   app.UseAuthentication();
///   app.UseAuthorization();
///
/// NOTA: usa lo schema di autenticazione standard di ASP.NET Core Identity,
/// che diventa lo scheme di default dell'applicazione. Va bene quando
/// l'host non ha un proprio Identity per il front-end (es. MyCms.TestHost).
/// Se in futuro un host avrà un proprio Identity separato, questo metodo
/// andrà esteso con uno SignInManager/scheme dedicato per evitare collisioni.
/// </summary>
public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddMyCmsIdentity(
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
            options.Cookie.Name = "MyCms.Auth";
            options.LoginPath = "/backoffice/account/login";
            options.AccessDeniedPath = "/backoffice/account/denied";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(CmsAuthConstants.AdminPolicy, p => p.RequireRole(CmsRoles.Admin))
            .AddPolicy(CmsAuthConstants.EditorPolicy, p => p.RequireRole(CmsRoles.Admin, CmsRoles.Editor));

        return services;
    }
}