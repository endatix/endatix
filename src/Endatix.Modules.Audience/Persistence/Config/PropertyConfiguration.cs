using Endatix.Core.Common;
using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config;

/// <summary>
/// Provider-agnostic mapping for <see cref="Property"/>.
/// </summary>
internal sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public const int DataTypeMaxLength = 32;

    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("Properties");
        ConfigureKeys(builder);
        ConfigureStrings(builder);
        builder.Property(property => property.SortOrder).IsRequired();
        builder.Property(property => property.AllowsOther).IsRequired();
        builder.HasIndex(property => new { property.TenantId, property.FormId })
            .HasDatabaseName("IX_Properties_Form");
    }

    private static void ConfigureKeys(EntityTypeBuilder<Property> builder)
    {
        builder.Property(property => property.TenantId).IsRequired();
        builder.Property(property => property.FormId).IsRequired();
    }

    private static void ConfigureStrings(EntityTypeBuilder<Property> builder)
    {
        builder.Property(property => property.VariableName)
            .HasMaxLength(DataSchemaConstants.MAX_SLUG_LENGTH)
            .IsRequired();
        builder.Property(property => property.Name)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();
        builder.Property(property => property.DataType)
            .HasMaxLength(DataTypeMaxLength)
            .IsRequired();
    }
}
