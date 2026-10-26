using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config;

/// <summary>
/// Mapping for <see cref="AudienceImportRejection"/>: one row per rejected spreadsheet row of an import.
/// </summary>
internal sealed class AudienceImportRejectionConfiguration(AudienceSqlDialect dialect)
    : IEntityTypeConfiguration<AudienceImportRejection>
{
    public void Configure(EntityTypeBuilder<AudienceImportRejection> builder)
    {
        builder.ToTable("ImportRejections", table => table.HasCheckConstraint(
            "CK_ImportRejections_TenantId", dialect.PositiveTenantIdCheck));

        builder.Property(rejection => rejection.TenantId).IsRequired();
        builder.Property(rejection => rejection.ImportId).IsRequired();
        builder.Property(rejection => rejection.Reason)
            .HasMaxLength(AudienceImportRejection.ReasonMaxLength)
            .IsRequired();
        builder.Property(rejection => rejection.CellsJson).HasColumnType(dialect.JsonColumnType).IsRequired();
        builder.HasOne<AudienceImport>()
            .WithMany()
            .HasForeignKey(rejection => rejection.ImportId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(rejection => new { rejection.ImportId, rejection.RowNumber })
            .IsUnique()
            .HasDatabaseName(AudienceImportRejection.UniqueConstraints.OnePerRow);
    }
}
