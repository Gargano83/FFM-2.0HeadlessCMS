using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DAMIHeadlessCMS.Core.Entities;

namespace DAMIHeadlessCMS.Data.Configurations;

public class CmsMenuConfiguration : IEntityTypeConfiguration<CmsMenu>
{
    public void Configure(EntityTypeBuilder<CmsMenu> builder)
    {
        builder.ToTable("Menu");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(m => m.Name)
            .IsUnique();

        builder.HasMany(m => m.Items)
            .WithOne(i => i.Menu)
            .HasForeignKey(i => i.MenuId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
