using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DAMIHeadlessCMS.Core.Entities;
using DAMIHeadlessCMS.Data.Configurations;
using DAMIHeadlessCMS.Data.Identity;

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

    public CmsDbContext(DbContextOptions<CmsDbContext> options) : base(options)
    {
    }

    public DbSet<EntityDefinition> EntityDefinitions => Set<EntityDefinition>();
    public DbSet<FieldDefinition> FieldDefinitions => Set<FieldDefinition>();
    public DbSet<CmsPage> Pages => Set<CmsPage>();
    public DbSet<CmsMenu> Menus => Set<CmsMenu>();
    public DbSet<CmsMenuItem> MenuItems => Set<CmsMenuItem>();

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
    }
}
