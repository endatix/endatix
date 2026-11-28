using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>()]
public class FormDefinitionConfiguration : IEntityTypeConfiguration<FormDefinition>
{
    public void Configure(EntityTypeBuilder<FormDefinition> builder)
    {
        builder.ToTable("FormDefinitions");

        builder.Property(fd => fd.Id)
            .IsRequired();

        builder.Property(fd => fd.JsonData)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();

        ConfigurePublication(builder);

        builder.HasOne<Form>()
            .WithMany(f => f.FormDefinitions)
            .IsRequired(false)
            .HasForeignKey(fd => fd.FormId);
    }

    private static void ConfigurePublication(EntityTypeBuilder<FormDefinition> builder)
    {
        builder.Property<string>("_publishedJson").HasColumnName("PublishedJson");
        builder.Property(fd => fd.ContentHash).IsRequired(false).UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("ContentHash")
            .HasMaxLength(FormDraftConfiguration.CONTENT_HASH_LENGTH).IsFixedLength().IsUnicode(false);
        // A detached legacy-shaped update must not match an already-published database row.
        builder.Property(fd => fd.PublishedFromDraftVersion).IsConcurrencyToken();
        builder.Property(fd => fd.Source).HasMaxLength(32).HasDefaultValue("publish");
    }
}
