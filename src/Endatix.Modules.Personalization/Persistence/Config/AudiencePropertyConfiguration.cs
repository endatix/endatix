using Endatix.Core.Common;
using Endatix.Modules.Personalization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Personalization.Persistence.Config;

/// <summary>
/// Provider-agnostic mapping for <see cref="AudienceProperty"/>.
/// </summary>
internal sealed class AudiencePropertyConfiguration : IEntityTypeConfiguration<AudienceProperty>
{
    public const int DataTypeMaxLength = 32;

    public void Configure(EntityTypeBuilder<AudienceProperty> builder)
    {
        builder.ToTable("AudienceProperties");

        builder.Property(property => property.TenantId).IsRequired();
        builder.Property(property => property.FormId).IsRequired();

        builder.Property(property => property.VariableName)
            .HasMaxLength(DataSchemaConstants.MAX_SLUG_LENGTH)
            .IsRequired();

        builder.Property(property => property.Name)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();

        builder.Property(property => property.DataType)
            .HasMaxLength(DataTypeMaxLength)
            .IsRequired();

        builder.Property(property => property.SortOrder).IsRequired();
        builder.Property(property => property.AllowsOther).IsRequired();

        builder.HasIndex(property => new { property.TenantId, property.FormId })
            .HasDatabaseName("IX_AudienceProperties_Form");
    }
}
