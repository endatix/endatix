using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppIdentity;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });

        builder.Property(rp => rp.GrantedAt)
            .IsRequired();

        builder.Property(rp => rp.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasOne<AppRole>()
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rp => new { rp.RoleId, rp.IsActive });
        builder.HasIndex(rp => rp.ExpiresAt);
    }
}