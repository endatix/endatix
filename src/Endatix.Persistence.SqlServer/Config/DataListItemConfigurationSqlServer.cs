using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Persistence.SqlServer.Config;

/// <summary>
/// SQL Server specific configuration for <see cref="DataListItem"/>.
/// </summary>
[ApplyConfigurationFor<AppDbContext>]
public sealed class DataListItemConfigurationSqlServer : IEntityTypeConfiguration<DataListItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DataListItem> builder)
    {
        builder.Property(x => x.LabelsJson)
            .HasColumnName("Labels")
            .HasColumnType("json");
    }
}
