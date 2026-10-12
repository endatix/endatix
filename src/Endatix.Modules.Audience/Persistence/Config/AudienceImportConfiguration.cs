using Endatix.Core.Common;
using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config;

/// <summary>
/// Provider-agnostic mapping for <see cref="AudienceImport"/>.
/// </summary>
internal sealed class AudienceImportConfiguration : IEntityTypeConfiguration<AudienceImport>
{
    public void Configure(EntityTypeBuilder<AudienceImport> builder)
    {
        builder.ToTable("Imports");

        builder.Property(import => import.TenantId).IsRequired();
        builder.Property(import => import.FormId).IsRequired();
        builder.Property(import => import.FileName)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();
        builder.Property(import => import.Status)
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(import => import.RejectedRowsBlobKey)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH);
        builder.HasIndex(import => new { import.TenantId, import.FormId });
    }
}
