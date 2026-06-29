using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Endatix.Infrastructure.Data.Config.AppEntities;

namespace Endatix.Persistence.PostgreSql.Config;

/// <summary>
/// PostgreSQL specific configuration for the <see cref="Folder"/> entity.
/// </summary>
[ApplyConfigurationFor<AppDbContext>]
public class FolderConfigurationPostgreSql : IEntityTypeConfiguration<Folder>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.Property(f => f.Metadata)
            .HasColumnType("jsonb");

        builder.HasIndex(f => new { f.TenantId, f.UrlSlug })
            .HasDatabaseName(FolderConfiguration.UniqueTenantSlugIndexName)
            .IsUnique()
            .HasFilter($"\"{nameof(Folder.IsDeleted)}\" = false");

        builder.HasIndex(f => new { f.TenantId, f.NormalizedName })
            .HasDatabaseName(FolderConfiguration.UniqueTenantNormalizedNameIndexName)
            .IsUnique()
            .HasFilter($"\"{nameof(Folder.IsDeleted)}\" = false");
    }
}
