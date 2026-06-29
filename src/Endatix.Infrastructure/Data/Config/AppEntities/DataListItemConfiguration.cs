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
        builder.Property(x => x.Label)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();
        builder.Property(x => x.Value)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();
        builder.HasIndex(x => x.DataListId);
    }
}
