using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>()]
public class FormTemplateConfiguration : IEntityTypeConfiguration<FormTemplate>
{
    public void Configure(EntityTypeBuilder<FormTemplate> builder)
    {
        builder.ToTable("FormTemplates");

        builder.Property(f => f.Id)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();

        builder.Property(fd => fd.JsonData)
            .IsRequired();

        builder.HasOne(t => t.Folder)
            .WithMany()
            .HasForeignKey(t => t.FolderId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => t.FolderId);
    }
}
