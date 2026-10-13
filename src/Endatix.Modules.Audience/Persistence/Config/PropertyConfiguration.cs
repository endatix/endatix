using Endatix.Core.Common;
using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config;

/// <summary>
/// Mapping for <see cref="Property"/>: JSON choices and a unique variable name per form.
/// </summary>
internal sealed class PropertyConfiguration(AudienceSqlDialect dialect) : IEntityTypeConfiguration<Property>
{
    public const int DataTypeMaxLength = 32;

    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("Properties", table => table.HasCheckConstraint(
            "CK_Properties_TenantId", dialect.PositiveTenantIdCheck));
        ConfigureKeys(builder);
        ConfigureStrings(builder);
        builder.Property(property => property.ChoicesJson).HasColumnType(dialect.JsonColumnType);
        builder.Property(property => property.SortOrder).IsRequired();
        builder.Property(property => property.AllowsOther).IsRequired();
        HasIndexes(builder);
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

    private void HasIndexes(EntityTypeBuilder<Property> builder)
    {
        builder.HasIndex(property => new { property.TenantId, property.FormId })
            .HasDatabaseName("IX_Properties_Form")
            .HasFilter(dialect.ActiveRowsFilter);

        builder.HasIndex(property => new { property.TenantId, property.FormId, property.VariableName })
            .IsUnique()
            .HasDatabaseName(Property.UniqueConstraints.VariableNamePerForm)
            .HasFilter(dialect.ActiveRowsFilter);
    }
}
