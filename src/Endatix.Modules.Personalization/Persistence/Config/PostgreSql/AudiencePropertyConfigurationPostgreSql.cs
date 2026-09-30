using Endatix.Modules.Personalization.Persistence;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Personalization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Personalization.Persistence.Config.PostgreSql;

/// <summary>
/// PostgreSQL mapping for <see cref="AudienceProperty"/>: jsonb choices and unique variable name.
/// </summary>
[ApplyConfigurationFor<PersonalizationPostgreSqlDbContext>]
internal sealed class AudiencePropertyConfigurationPostgreSql : IEntityTypeConfiguration<AudienceProperty>
{
    public void Configure(EntityTypeBuilder<AudienceProperty> builder)
    {
        builder.Property(property => property.ChoicesJson)
            .HasColumnType("jsonb");

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_AudienceProperties_TenantId",
            $"\"{nameof(AudienceProperty.TenantId)}\" > 0"));

        builder.HasIndex(property => new { property.TenantId, property.FormId, property.VariableName })
            .IsUnique()
            .HasDatabaseName("IX_AudienceProperties_VariableName")
            .HasFilter($"\"{nameof(AudienceProperty.IsDeleted)}\" = false");
    }
}
