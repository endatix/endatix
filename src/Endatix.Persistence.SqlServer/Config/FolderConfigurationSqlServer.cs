using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Endatix.Infrastructure.Data.Config.AppEntities;

namespace Endatix.Persistence.SqlServer.Config;

[ApplyConfigurationFor<AppDbContext>]
public class FolderConfigurationSqlServer : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.Property(f => f.Metadata)
            .HasColumnType("json");

        builder.HasIndex(f => new { f.TenantId, f.UrlSlug })
            .HasDatabaseName(FolderConfiguration.UniqueTenantSlugIndexName)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
