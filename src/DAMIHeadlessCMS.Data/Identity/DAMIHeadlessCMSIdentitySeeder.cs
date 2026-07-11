using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DAMIHeadlessCMS.Data.Identity;

/// <summary>
/// Crea i ruoli di base (CmsAdmin/CmsEditor/CmsOperator) e, se configurati, i
/// primi utenti per ciascun ruolo — indispensabile almeno per l'admin al
/// primo avvio: senza un admin pre-esistente nessuno potrebbe accedere al
/// backoffice per crearne uno.
///
/// Configurazione opzionale in appsettings (o appsettings.Development.json,
/// MAI committata con credenziali reali):
///   "DAMIHeadlessCMS": {
///     "SeedAdmin":    { "Email": "admin@example.com",    "Password": "Pa$$w0rd1" },
///     "SeedEditor":   { "Email": "editor@example.com",   "Password": "Pa$$w0rd1" },
///     "SeedOperator": { "Email": "operator@example.com", "Password": "Pa$$w0rd1" }
///   }
/// Ogni blocco è indipendente e facoltativo: se Email/Password non sono
/// entrambe valorizzate per un ruolo, il relativo utente non viene creato.
/// </summary>
public static class DAMIHeadlessCMSIdentitySeeder
{
    /// <summary>Un utente da seedare per un dato ruolo, con la relativa chiave di configurazione.</summary>
    private sealed record SeedUserSpec(string ConfigKey, string RoleName, string DefaultDisplayName);

    private static readonly SeedUserSpec[] SeedSpecs =
    {
        new("SeedAdmin", CmsRoles.Admin, "Amministratore"),
        new("SeedEditor", CmsRoles.Editor, "Editor"),
        new("SeedOperator", CmsRoles.Operator, "Operatore")
    };

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

        foreach (var spec in SeedSpecs)
        {
            await SeedUserAsync(userManager, configuration, spec);
        }
    }

    private static async Task SeedUserAsync(UserManager<CmsUser> userManager, IConfiguration configuration, SeedUserSpec spec)
    {
        var seedEmail = configuration[$"DAMIHeadlessCMS:{spec.ConfigKey}:Email"];
        var seedPassword = configuration[$"DAMIHeadlessCMS:{spec.ConfigKey}:Password"];

        if (string.IsNullOrWhiteSpace(seedEmail) || string.IsNullOrWhiteSpace(seedPassword))
        {
            return;
        }

        var existing = await userManager.FindByEmailAsync(seedEmail);
        if (existing is not null)
        {
            // L'utente esiste già (es. creato manualmente in un secondo momento):
            // ci si limita a garantire l'appartenenza al ruolo previsto, senza
            // toccare password o altri dati.
            if (!await userManager.IsInRoleAsync(existing, spec.RoleName))
            {
                await userManager.AddToRoleAsync(existing, spec.RoleName);
            }
            return;
        }

        var user = new CmsUser
        {
            UserName = seedEmail,
            Email = seedEmail,
            EmailConfirmed = true,
            DisplayName = spec.DefaultDisplayName
        };

        var createResult = await userManager.CreateAsync(user, seedPassword);
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(user, spec.RoleName);
        }
    }
}