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

        builder.Property(e => e.DetailRoutePrefix)
            .HasMaxLength(200);

        builder.HasIndex(e => new { e.SchemaName, e.TableName })
            .IsUnique();

        // Indice univoco filtrato: due entità non possono condividere lo
        // stesso prefisso di routing di dettaglio. Complementare (non
        // sostitutivo) al controllo applicativo in ScaffoldingWizardController,
        // che verifica anche contro CmsPage.Slug e i percorsi ExternalUrl dei
        // menu — cose che un indice DB su questa sola tabella non può sapere.
        builder.HasIndex(e => e.DetailRoutePrefix)
            .IsUnique()
            .HasFilter("[DetailRoutePrefix] IS NOT NULL");

        builder.HasMany(e => e.Fields)
            .WithOne(f => f.EntityDefinition)
            .HasForeignKey(f => f.EntityDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        // NoAction (non SetNull): SQL Server considera "cascading" anche SET
        // NULL, non solo CASCADE — quindi una seconda relazione FK nella
        // direzione opposta tra le stesse due tabelle (FieldDefinition ->
        // EntityDefinition è già una cascata, sopra) causa comunque l'errore
        // "may cause cycles or multiple cascade paths", anche con SetNull.
        // Solo NoAction ne è esente.
        // Nota: oggi non esiste alcun percorso applicativo che elimini un
        // FieldDefinition (lo scaffolding è additivo/idempotente, non rimuove
        // mai righe per colonne non più presenti — vedi ScaffoldingService),
        // quindi il caso "riferimento pendente dopo eliminazione" non si
        // verifica in pratica. Se in futuro venisse introdotta
        // un'eliminazione esplicita dei campi, andrà gestita ripulendo
        // esplicitamente eventuali EntityDefinition.DetailKeyFieldId che vi
        // puntano, dato che qui il database non lo fa più da solo.
        builder.HasOne(e => e.DetailKeyField)
            .WithMany()
            .HasForeignKey(e => e.DetailKeyFieldId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Ignore(e => e.QualifiedTableName);
    }
}
