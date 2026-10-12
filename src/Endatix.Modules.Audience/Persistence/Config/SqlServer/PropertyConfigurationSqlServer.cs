using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config.SqlServer;

/// <summary>
/// SQL Server mapping for <see cref="Property"/>: json choices and unique variable name.
/// </summary>
[ApplyConfigurationFor<AudienceSqlServerDbContext>]
internal sealed class PropertyConfigurationSqlServer : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.Property(property => property.ChoicesJson)
            .HasColumnType("json");

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Properties_TenantId",
            $"[{nameof(Property.TenantId)}] > 0"));

        builder.HasIndex(property => new { property.TenantId, property.FormId })
            .HasDatabaseName("IX_Properties_Form")
            .HasFilter($"[{nameof(Property.IsDeleted)}] = 0");

        builder.HasIndex(property => new { property.TenantId, property.FormId, property.VariableName })
            .IsUnique()
            .HasDatabaseName(Property.UniqueConstraints.VariableNamePerForm)
            .HasFilter($"[{nameof(Property.IsDeleted)}] = 0");
    }
}
