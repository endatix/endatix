using Endatix.Modules.Personalization.Persistence;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Personalization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Personalization.Persistence.Config.PostgreSql;

/// <summary>
/// PostgreSQL mapping for <see cref="Property"/>: jsonb choices and unique variable name.
/// </summary>
[ApplyConfigurationFor<PersonalizationPostgreSqlDbContext>]
internal sealed class PropertyConfigurationPostgreSql : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.Property(property => property.ChoicesJson)
            .HasColumnType("jsonb");

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Properties_TenantId",
            $"\"{nameof(Property.TenantId)}\" > 0"));

        builder.HasIndex(property => new { property.TenantId, property.FormId, property.VariableName })
            .IsUnique()
            .HasDatabaseName("IX_Properties_VariableName")
            .HasFilter($"\"{nameof(Property.IsDeleted)}\" = false");
    }
}
