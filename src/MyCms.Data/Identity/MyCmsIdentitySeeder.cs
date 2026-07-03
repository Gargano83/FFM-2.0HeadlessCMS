using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyCms.Data.Identity;

/// <summary>
/// Crea i ruoli di base (CmsAdmin/CmsEditor) e, se configurato, un primo
/// utente amministratore — indispensabile al primo avvio: senza un admin
/// pre-esistente nessuno potrebbe accedere al backoffice per crearne uno.
///
/// Configurazione opzionale in appsettings (o appsettings.Development.json,
/// MAI committata con credenziali reali):
///   "MyCms": { "SeedAdmin": { "Email": "admin@example.com", "Password": "Pa$$w0rd1" } }
/// </summary>
public static class MyCmsIdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<CmsRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<CmsUser>>();

        foreach (var roleName in CmsRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new CmsRole(roleName));
            }
        }

        var seedEmail = configuration["MyCms:SeedAdmin:Email"];
        var seedPassword = configuration["MyCms:SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(seedEmail) || string.IsNullOrWhiteSpace(seedPassword))
        {
            return;
        }

        var existing = await userManager.FindByEmailAsync(seedEmail);
        if (existing is not null)
        {
            return;
        }

        var admin = new CmsUser
        {
            UserName = seedEmail,
            Email = seedEmail,
            EmailConfirmed = true,
            DisplayName = "Amministratore"
        };

        var createResult = await userManager.CreateAsync(admin, seedPassword);
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, CmsRoles.Admin);
        }
    }
}