using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>()]
public class DataListConfiguration : IEntityTypeConfiguration<DataList>
{
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

        builder.HasIndex(x => new { x.TenantId, x.Name })
            .IsUnique();

        builder.HasMany(x => x.Items)
            .WithOne(x => x.DataList)
            .HasForeignKey(x => x.DataListId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
