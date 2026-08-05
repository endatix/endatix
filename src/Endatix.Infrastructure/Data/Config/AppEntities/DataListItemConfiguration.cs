using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>()]
public class DataListItemConfiguration : IEntityTypeConfiguration<DataListItem>
{
    public void Configure(EntityTypeBuilder<DataListItem> builder)
    {
        builder.ToTable("DataListItems");

        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.DataListId).IsRequired();

        // Store-friendly JSON column (provider overrides set jsonb / nvarchar).
        // Named "Labels" so migrations and JSON-path SQL stay stable.
        builder.Property(x => x.LabelsJson)
            .HasColumnName("Labels")
            .HasColumnType("json")
            .IsRequired();

        builder.Property(x => x.Value)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();

        builder.Ignore(x => x.Labels);
        builder.Ignore(x => x.DefaultLabel);

        builder.HasIndex(x => x.DataListId);
    }
}
