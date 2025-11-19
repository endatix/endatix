using Endatix.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppIdentity;

[ApplyConfigurationFor<AppIdentityDbContext>()]
public class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    private const string BASE_ROLE_NAME_INDEX_NAME = "RoleNameIndex";

    public void Configure(EntityTypeBuilder<AppRole> builder)
    {
        builder.Property(r => r.Description)
            .HasMaxLength(DataSchemaConstants.MAX_DESCRIPTION_LENGTH);

        builder.Property(r => r.TenantId)
            .IsRequired();

        builder.Property(r => r.IsSystemDefined)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasMany(r => r.RolePermissions)
            .WithOne()
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(r => r.EffectivePermissions);

        var baseRoleNameIndex = builder
            .Metadata.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == BASE_ROLE_NAME_INDEX_NAME);
        if (baseRoleNameIndex != null)
        {
            builder.Metadata.RemoveIndex(baseRoleNameIndex);
        }

        builder.HasIndex(r => new
        {
            r.NormalizedName,
            r.TenantId
        })
        .HasDatabaseName("IX_AppRole_NormalizedName_TenantId")
        .IsUnique();
    }
}