using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyCms.Core.Entities;

namespace MyCms.Data.Configurations;

public class CmsMenuItemConfiguration : IEntityTypeConfiguration<CmsMenuItem>
{
    public void Configure(EntityTypeBuilder<CmsMenuItem> builder)
    {
        builder.ToTable("MenuItem");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Label)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.TargetType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(i => i.TargetValue)
            .HasMaxLength(500)
            .IsRequired();

        // Struttura ad albero: un item può avere figli (sotto-menu).
        builder.HasOne(i => i.Parent)
            .WithMany(i => i.Children)
            .HasForeignKey(i => i.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
