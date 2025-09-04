using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;

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
            .IsRequired();

        builder.HasOne<Form>()
            .WithMany(f => f.FormDefinitions)
            .IsRequired(false)
            .HasForeignKey(fd => fd.FormId);
    }
}
