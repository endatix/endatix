using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppIdentity;

[ApplyConfigurationFor<AppIdentityDbContext>()]
public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.Property(p => p.Id)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(DataSchemaConstants.MAX_DESCRIPTION_LENGTH);

        builder.Property(p => p.Category)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH);

        builder.Property(p => p.IsSystemDefined)
        .IsRequired();

        builder.Property(p => p.IsActive)
            .IsRequired();

        builder.HasIndex(p => p.Name)
            .IsUnique();

        builder.HasIndex(p => p.Category);
    }
}