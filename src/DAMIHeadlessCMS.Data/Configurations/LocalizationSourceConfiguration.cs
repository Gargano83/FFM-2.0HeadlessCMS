using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DAMIHeadlessCMS.Core.Entities;

namespace DAMIHeadlessCMS.Data.Configurations;

public class LocalizationSourceConfiguration : IEntityTypeConfiguration<LocalizationSource>
{
    public void Configure(EntityTypeBuilder<LocalizationSource> builder)
    {
        builder.ToTable("LocalizationSource");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.DisplayName).HasMaxLength(200).IsRequired();

        builder.Property(s => s.ContentSchemaName).HasMaxLength(128).IsRequired();
        builder.Property(s => s.ContentTableName).HasMaxLength(128).IsRequired();
        builder.Property(s => s.ContentIdColumn).HasMaxLength(128).IsRequired();
        builder.Property(s => s.LanguageIdColumn).HasMaxLength(128).IsRequired();
        builder.Property(s => s.TextColumn).HasMaxLength(128).IsRequired();
        builder.Property(s => s.RowIdColumn).HasMaxLength(128);

        builder.Property(s => s.LanguageSchemaName).HasMaxLength(128).IsRequired();
        builder.Property(s => s.LanguageTableName).HasMaxLength(128).IsRequired();
        builder.Property(s => s.LanguageIdColumnInLanguageTable).HasMaxLength(128).IsRequired();
        builder.Property(s => s.LanguageCodeColumn).HasMaxLength(128);
        builder.Property(s => s.LanguageNameColumn).HasMaxLength(128);
    }
}