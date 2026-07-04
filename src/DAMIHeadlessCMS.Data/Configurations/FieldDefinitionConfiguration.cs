using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DAMIHeadlessCMS.Core.Entities;

namespace DAMIHeadlessCMS.Data.Configurations;

public class FieldDefinitionConfiguration : IEntityTypeConfiguration<FieldDefinition>
{
    public void Configure(EntityTypeBuilder<FieldDefinition> builder)
    {
        builder.ToTable("FieldDefinition");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.ColumnName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(f => f.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(f => f.SqlDataType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(f => f.ForeignKeyDisplayColumn)
            .HasMaxLength(128);

        builder.Property(f => f.EditorType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(f => new { f.EntityDefinitionId, f.ColumnName })
            .IsUnique();

        // FK verso l'entità di destinazione (usata per i campi Select su FK).
        // Restrict per evitare cicli di cascade delete tra EntityDefinition.
        builder.HasOne(f => f.ForeignKeyTargetEntity)
            .WithMany()
            .HasForeignKey(f => f.ForeignKeyTargetEntityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
