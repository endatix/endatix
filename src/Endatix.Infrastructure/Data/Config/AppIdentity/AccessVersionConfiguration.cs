using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppIdentity;

/// <summary>Maps <see cref="AccessVersion"/>, one row per tenant; rows are inserted on their first change, never seeded with a shared value.</summary>
[ApplyConfigurationFor<AppIdentityDbContext>()]
internal sealed class AccessVersionConfiguration : IEntityTypeConfiguration<AccessVersion>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AccessVersion> builder)
    {
        builder.ToTable("AccessVersions");

        builder.HasKey(version => version.TenantId);

        builder.Property(version => version.TenantId)
            .ValueGeneratedNever();

        builder.Property(version => version.Value)
            .IsRequired();
    }
}
