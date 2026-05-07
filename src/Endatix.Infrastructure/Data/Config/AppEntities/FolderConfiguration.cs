using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>()]
public class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public const string UniqueTenantSlugIndexName = Folder.UniqueConstraints.UrlSlugPerTenant;

    public const string UniqueTenantNormalizedNameIndexName = Folder.UniqueConstraints.NormalizedNamePerTenant;

    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.ToTable("Folders");

        builder.Property(f => f.Id)
            .IsRequired();

        builder.Property(f => f.Name)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();

        builder.Property(f => f.NormalizedName)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();

        builder.Property(f => f.UrlSlug)
            .HasMaxLength(DataSchemaConstants.MAX_SLUG_LENGTH)
            .IsRequired();

        builder.Property(f => f.Description)
            .HasMaxLength(DataSchemaConstants.MAX_DESCRIPTION_LENGTH)
            .IsRequired(false);

        builder.HasOne(f => f.ParentFolder)
            .WithMany()
            .HasForeignKey(f => f.ParentFolderId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.Tenant)
            .WithMany()
            .HasForeignKey(f => f.TenantId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
