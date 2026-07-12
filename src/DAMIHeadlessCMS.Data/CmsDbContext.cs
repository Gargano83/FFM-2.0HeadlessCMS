using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using DAMIHeadlessCMS.Core.Entities;
using DAMIHeadlessCMS.Data.Configurations;
using DAMIHeadlessCMS.Data.Identity;
using System.Security.Claims;

namespace DAMIHeadlessCMS.Data;

/// <summary>
/// DbContext dedicato alle tabelle di sistema del CMS (metadati di scaffolding,
/// pagine custom, menu). Vive nello schema "cms" per non entrare in conflitto
/// con le tabelle applicative dell'app host.
///
/// Le entità gestite dal CRUD generico (Products, Orders, ecc.) NON sono
/// mappate qui: vengono lette/scritte dinamicamente da IGenericEntityRepository
/// (fase 3 della roadmap) tramite SQL parametrico guidato dai metadati.
/// </summary>
public class CmsDbContext : IdentityDbContext<CmsUser, CmsRole, Guid>
{
    public const string Schema = "cms";

    private readonly IHttpContextAccessor? _httpContextAccessor;

    public CmsDbContext(DbContextOptions<CmsDbContext> options) : this(options, null)
    {
    }

    /// <summary>
    /// Overload usato dal container DI dell'app host: <c>IHttpContextAccessor</c>
    /// serve a <see cref="BuildAuditEntries"/> per sapere chi sta salvando.
    /// Resta opzionale (può essere null, es. per gli strumenti design-time di
    /// <c>dotnet ef</c> che non passano da un host web) perché il DbContext
    /// deve restare utilizzabile anche fuori da una richiesta HTTP.
    /// </summary>
    public CmsDbContext(DbContextOptions<CmsDbContext> options, IHttpContextAccessor? httpContextAccessor) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<EntityDefinition> EntityDefinitions => Set<EntityDefinition>();
    public DbSet<FieldDefinition> FieldDefinitions => Set<FieldDefinition>();
    public DbSet<CmsPage> Pages => Set<CmsPage>();
    public DbSet<CmsMenu> Menus => Set<CmsMenu>();
    public DbSet<CmsMenuItem> MenuItems => Set<CmsMenuItem>();
    public DbSet<LocalizationSource> LocalizationSources => Set<LocalizationSource>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        base.OnModelCreating(modelBuilder);

        // Rinomino le tabelle Identity per coerenza con la naming convention
        // del resto dello schema cms (niente prefisso "AspNet").
        modelBuilder.Entity<CmsUser>(b =>
        {
            b.ToTable("User");
            b.Property(u => u.DisplayName).HasMaxLength(200);
        });
        modelBuilder.Entity<CmsRole>(b => b.ToTable("Role"));
        modelBuilder.Entity<IdentityUserRole<Guid>>(b => b.ToTable("UserRole"));
        modelBuilder.Entity<IdentityUserClaim<Guid>>(b => b.ToTable("UserClaim"));
        modelBuilder.Entity<IdentityUserLogin<Guid>>(b => b.ToTable("UserLogin"));
        modelBuilder.Entity<IdentityRoleClaim<Guid>>(b => b.ToTable("RoleClaim"));
        modelBuilder.Entity<IdentityUserToken<Guid>>(b => b.ToTable("UserToken"));

        modelBuilder.ApplyConfiguration(new EntityDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new FieldDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new CmsPageConfiguration());
        modelBuilder.ApplyConfiguration(new CmsMenuConfiguration());
        modelBuilder.ApplyConfiguration(new CmsMenuItemConfiguration());
        modelBuilder.ApplyConfiguration(new LocalizationSourceConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogEntryConfiguration());
    }

    /// <summary>
    /// Genera automaticamente le righe di <see cref="AuditLogEntry"/> per le
    /// entità CMS-native modificate in questo salvataggio (vedi
    /// <see cref="BuildAuditEntries"/> per lo scope esatto), poi salva tutto
    /// — incluse le righe di audit stesse — in una singola transazione
    /// implicita di EF Core. Nessun controller deve occuparsene esplicitamente:
    /// funziona anche per le scritture fatte tramite UserManager/RoleManager
    /// (Identity), che usano questo stesso DbContext internamente.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var auditEntries = BuildAuditEntries();
        if (auditEntries.Count > 0)
        {
            AuditLogEntries.AddRange(auditEntries);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    private List<AuditLogEntry> BuildAuditEntries()
    {
        var entries = new List<AuditLogEntry>();
        var (userId, userEmail) = GetCurrentUser();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var described = Describe(entry);
            if (described is null)
            {
                // Entità fuori scope per l'audit (es. tabelle di supporto di
                // Identity come UserClaim/UserToken, o l'AuditLogEntry stessa).
                continue;
            }

            var (entityType, entityId, summary) = described.Value;

            entries.Add(new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow,
                UserId = userId,
                UserEmail = userEmail,
                EntityType = entityType,
                EntityId = entityId,
                Action = entry.State switch
                {
                    EntityState.Added => "Create",
                    EntityState.Modified => "Update",
                    EntityState.Deleted => "Delete",
                    _ => "Unknown"
                },
                Summary = summary
            });
        }

        return entries;
    }

    /// <summary>
    /// Scope dell'audit: solo le entità CMS-native elencate qui sotto (Pagine,
    /// Menu, voci di Menu, Utenti). Tutto il resto — incluse le tabelle di
    /// supporto di Identity (ruoli assegnati, claim, token) e ovviamente i
    /// dati scaffoldati nella sezione "Dati" (fuori dal ChangeTracker, letti
    /// via ADO.NET) — resta fuori per non generare rumore/dati sensibili
    /// (es. i token di Identity) nel log.
    /// </summary>
    private static (string entityType, string entityId, string? summary)? Describe(EntityEntry entry)
    {
        return entry.Entity switch
        {
            CmsPage page => ("CmsPage", page.Id.ToString(), $"Pagina \"{page.Title}\" ({page.Slug})"),
            CmsMenu menu => ("CmsMenu", menu.Id.ToString(), $"Menu \"{menu.Name}\""),
            CmsMenuItem item => ("CmsMenuItem", item.Id.ToString(), $"Voce di menu \"{item.Label}\""),
            CmsUser user => ("CmsUser", user.Id.ToString(), $"Utente {user.Email}"),
            _ => null
        };
    }

    private (Guid? userId, string? userEmail) GetCurrentUser()
    {
        var principal = _httpContextAccessor?.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return (null, null);
        }

        var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.Identity.Name;

        return (Guid.TryParse(idClaim, out var parsed) ? parsed : null, email);
    }
}
