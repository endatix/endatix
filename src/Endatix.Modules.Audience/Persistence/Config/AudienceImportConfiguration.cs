using Endatix.Core.Common;
using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config;

/// <summary>
/// Mapping for <see cref="AudienceImport"/>.
/// </summary>
internal sealed class AudienceImportConfiguration(AudienceSqlDialect dialect)
    : IEntityTypeConfiguration<AudienceImport>
{
    public void Configure(EntityTypeBuilder<AudienceImport> builder)
    {
        builder.ToTable("Imports", table => table.HasCheckConstraint(
            "CK_Imports_TenantId", dialect.PositiveTenantIdCheck));

        builder.Property(import => import.TenantId).IsRequired();
        builder.Property(import => import.FormId).IsRequired();
        builder.Property(import => import.FileName)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();
        // Every transition is a compare-and-set on the status: a cancel and a finishing Check cannot both win.
        builder.Property(import => import.Status)
            .HasMaxLength(32)
            .IsRequired()
            .IsConcurrencyToken();
        builder.Property(import => import.RejectedRowsBlobKey)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH);
        HasStagedImportColumns(builder);
        HasIndexes(builder);
    }

    /// <summary>What a staged import is fixed to when it starts, and the members that are read, not stored.</summary>
    private void HasStagedImportColumns(EntityTypeBuilder<AudienceImport> builder)
    {
        builder.Property(import => import.HeadersJson).HasColumnType(dialect.JsonColumnType);
        builder.Property(import => import.PropertyColumnsJson).HasColumnType(dialect.JsonColumnType);
        builder.Property(import => import.IdentifierColumn).HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH);
        builder.Property(import => import.IdentifierKind).HasMaxLength(32);
        builder.Property(import => import.Failure).HasMaxLength(AudienceImport.FailureMaxLength);
        builder.Ignore(import => import.Headers);
        builder.Ignore(import => import.PropertyColumns);
        builder.Ignore(import => import.ChunkCount);
        builder.Ignore(import => import.IsUnfinished);
    }

    private void HasIndexes(EntityTypeBuilder<AudienceImport> builder)
    {
        builder.HasIndex(import => new { import.TenantId, import.FormId });

        // One import a form can still change. The previous release only inserts completed rows, which the filter
        // never matches, so it can ship in the release that starts writing unfinished ones.
        builder.HasIndex(import => new { import.TenantId, import.FormId }, AudienceImport.UniqueConstraints.OneUnfinishedPerForm)
            .IsUnique()
            .HasFilter(dialect.UnfinishedImportsFilter);
    }
}
