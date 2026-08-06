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
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DataList> builder)
    {
        builder.ToTable("DataLists");

        builder.Property(x => x.Id).IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();

        builder.Property(x => x.NormalizedName)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(DataSchemaConstants.MAX_DESCRIPTION_LENGTH)
            .IsRequired(false);

        builder.Property(x => x.DefaultLocale)
            .HasMaxLength(DataSchemaConstants.MAX_CULTURE_CODE_LENGTH)
            .IsRequired();

        builder.PrimitiveCollection(x => x.AvailableLocales)
            .HasField("_availableLocales")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .ElementType()
            .HasMaxLength(DataSchemaConstants.MAX_CULTURE_CODE_LENGTH);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.DataList)
            .HasForeignKey(x => x.DataListId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
