using Endatix.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppIdentity;

[ApplyConfigurationFor<AppIdentityDbContext>()]
public class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
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

        // Indexes
        builder.HasIndex(r => r.TenantId);

        builder.HasIndex(r => new { r.TenantId, r.Name })
            .IsUnique();

        builder.HasIndex(r => r.IsActive);

        // Create tenant-scoped unique index on NormalizedName
        // This replaces ASP.NET Identity's default global RoleNameIndex
        // with a tenant-scoped version for multi-tenancy support
        builder.HasIndex(r => new { r.TenantId, r.NormalizedName })
            .HasDatabaseName("IX_Roles_TenantId_NormalizedName")
            .IsUnique();
    }
}
