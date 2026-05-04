using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

/// <summary>
/// Configuration for the <see cref="DataList"/> entity.
/// </summary>
[ApplyConfigurationFor<AppDbContext>()]
public sealed class DataListConfiguration : IEntityTypeConfiguration<DataList>
{
    /// <summary>
    /// The name of the unique index on the <see cref="DataList.Name"/> column.
    /// </summary>
    public const string UNIQUE_DATA_LIST_NAME_INDEX_NAME = "IX_Unique_DataList_Name";

    /// <inheritdoc />
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

        builder.HasMany(x => x.Items)
            .WithOne(x => x.DataList)
            .HasForeignKey(x => x.DataListId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
