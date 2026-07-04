using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DAMIHeadlessCMS.Core.Entities;

namespace DAMIHeadlessCMS.Data.Configurations;

public class EntityDefinitionConfiguration : IEntityTypeConfiguration<EntityDefinition>
{
    public void Configure(EntityTypeBuilder<EntityDefinition> builder)
    {
        builder.ToTable("EntityDefinition");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TableName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.SchemaName)
            .HasMaxLength(128)
            .IsRequired()
            .HasDefaultValue("dbo");

        builder.Property(e => e.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.PrimaryKeyColumn)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.Icon)
            .HasMaxLength(50);

        builder.HasIndex(e => new { e.SchemaName, e.TableName })
            .IsUnique();

        builder.HasMany(e => e.Fields)
            .WithOne(f => f.EntityDefinition)
            .HasForeignKey(f => f.EntityDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(e => e.QualifiedTableName);
    }
}
