using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>()]
public sealed class DataListConfiguration : IEntityTypeConfiguration<DataList>, IDatabaseProviderAwareConfiguration
{
    private string? _databaseProviderName;

    /// <inheritdoc />
    public void SetDatabaseProviderName(string? databaseProviderName)
    {
        _databaseProviderName = databaseProviderName;
    }

    public void Configure(EntityTypeBuilder<DataList> builder)
    {
        builder.ToTable("DataLists");

        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.Name)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();
        builder.Property(x => x.Description)
            .HasMaxLength(DataSchemaConstants.MAX_DESCRIPTION_LENGTH)
            .IsRequired(false);

        var indexBuilder = builder.HasIndex(x => new { x.TenantId, x.Name })
            .IsUnique();

        if (EfCoreDatabaseProviders.IsSqlServer(_databaseProviderName))
        {
            indexBuilder.HasFilter("[IsDeleted] = 0");
        }
        else
        {
            indexBuilder.HasFilter($"\"{nameof(DataList.IsDeleted)}\" = false");
        }

        builder.HasMany(x => x.Items)
            .WithOne(x => x.DataList)
            .HasForeignKey(x => x.DataListId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
