using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>()]
public class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public const string UniqueTenantSlugIndexName = "IX_Folders_TenantId_Slug_Unique";

    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.ToTable("Folders");

        builder.Property(f => f.Id)
            .IsRequired();

        builder.Property(f => f.Name)
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
    }
}
