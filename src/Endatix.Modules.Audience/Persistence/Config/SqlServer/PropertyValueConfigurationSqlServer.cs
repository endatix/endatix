using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config.SqlServer;

/// <summary>
/// SQL Server mapping for <see cref="PropertyValue"/>: one cell per property on a membership.
/// </summary>
[ApplyConfigurationFor<AudienceSqlServerDbContext>]
internal sealed class PropertyValueConfigurationSqlServer : IEntityTypeConfiguration<PropertyValue>
{
    public void Configure(EntityTypeBuilder<PropertyValue> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_PropertyValues_TenantId",
            $"[{nameof(PropertyValue.TenantId)}] > 0"));

        builder.HasIndex(value => new { value.TenantId, value.MembershipId, value.PropertyId })
            .IsUnique()
            .HasDatabaseName(PropertyValue.UniqueConstraints.CellPerMembership)
            .HasFilter($"[{nameof(PropertyValue.IsDeleted)}] = 0");

        builder.HasIndex(value => new { value.TenantId, value.PropertyId })
            .HasDatabaseName("IX_PropertyValues_Property")
            .HasFilter($"[{nameof(PropertyValue.IsDeleted)}] = 0");
    }
}
