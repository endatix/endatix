using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Endatix.Infrastructure.Data.Config.AppEntities;

namespace Endatix.Persistence.SqlServer.Config
{
    /// <summary>
    /// SQL Server specific configuration for the <see cref="DataList"/> entity.
    /// </summary>
    [ApplyConfigurationFor<AppDbContext>]
    public class DataListConfigurationSqlServer : IEntityTypeConfiguration<DataList>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<DataList> builder)
        {
            builder.HasIndex(x => new { x.TenantId, x.Name })
                .HasDatabaseName(DataListConfiguration.UNIQUE_DATA_LIST_NAME_INDEX_NAME)
                .IsUnique()
                .HasFilter($"[{nameof(DataList.IsDeleted)}] = 0");
        }
    }
}
