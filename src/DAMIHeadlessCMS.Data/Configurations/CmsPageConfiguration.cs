using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DAMIHeadlessCMS.Core.Entities;

namespace DAMIHeadlessCMS.Data.Configurations;

public class CmsPageConfiguration : IEntityTypeConfiguration<CmsPage>
{
    public void Configure(EntityTypeBuilder<CmsPage> builder)
    {
        builder.ToTable("Page");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Slug)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(p => p.Slug)
            .IsUnique();

        builder.Property(p => p.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.ContentJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired()
            .HasDefaultValue("[]");

        builder.HasOne(p => p.Parent)
            .WithMany()
            .HasForeignKey(p => p.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");
    }
}
