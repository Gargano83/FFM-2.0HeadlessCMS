using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DAMIHeadlessCMS.Core.Entities;

namespace DAMIHeadlessCMS.Data.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntry");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserEmail).HasMaxLength(256);
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Summary).HasMaxLength(500);

        // Query tipica della dashboard: "ultime N righe", quindi ordinamento
        // discendente per data — indice a supporto di quell'accesso.
        builder.HasIndex(a => a.TimestampUtc);
    }
}
