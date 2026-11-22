using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

/// <summary>
/// Maps <see cref="TenantBrandingVersion"/>: the key is the tenant and the number, so a second version with the same
/// number (a lost race) is a unique violation.
/// </summary>
[ApplyConfigurationFor<AppDbContext>()]
internal sealed class TenantBrandingVersionConfiguration : IEntityTypeConfiguration<TenantBrandingVersion>
{
    // Core's CanonicalContentHash.LENGTH (SHA-256 as hex) and ContentVersionSources.MAX_LENGTH, which are internal to Core.
    private const int CONTENT_HASH_LENGTH = 64;
    private const int SOURCE_MAX_LENGTH = 32;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TenantBrandingVersion> builder)
    {
        builder.ToTable("TenantBrandingVersions");

        builder.HasKey(version => new { version.TenantId, version.Number });

        builder.Property(version => version.ContentHash)
            .HasMaxLength(CONTENT_HASH_LENGTH)
            .IsFixedLength()
            .IsRequired();

        builder.Property(version => version.Source)
            .HasMaxLength(SOURCE_MAX_LENGTH)
            .IsRequired();

        BelongsToTenant(builder);
    }

    private static void BelongsToTenant(EntityTypeBuilder<TenantBrandingVersion> builder) =>
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(version => version.TenantId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
}
