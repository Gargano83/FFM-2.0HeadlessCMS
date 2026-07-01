using Microsoft.EntityFrameworkCore;
using MyCms.Core.Entities;
using MyCms.Data.Configurations;

namespace MyCms.Data;

/// <summary>
/// DbContext dedicato alle tabelle di sistema del CMS (metadati di scaffolding,
/// pagine custom, menu). Vive nello schema "cms" per non entrare in conflitto
/// con le tabelle applicative dell'app host.
///
/// Le entità gestite dal CRUD generico (Products, Orders, ecc.) NON sono
/// mappate qui: vengono lette/scritte dinamicamente da IGenericEntityRepository
/// (fase 3 della roadmap) tramite SQL parametrico guidato dai metadati.
/// </summary>
public class CmsDbContext : DbContext
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

        modelBuilder.ApplyConfiguration(new EntityDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new FieldDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new CmsPageConfiguration());
        modelBuilder.ApplyConfiguration(new CmsMenuConfiguration());
        modelBuilder.ApplyConfiguration(new CmsMenuItemConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
