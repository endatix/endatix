using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Data.Config
{
    public class FormConfiguration : IEntityTypeConfiguration<Form>
    {
        public void Configure(EntityTypeBuilder<Form> builder)
        {
            builder.ToTable("Forms");

            builder.Property(f => f.Id)
                .IsRequired();

            builder.Property(p => p.Name)
                .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
                .IsRequired();

            builder.HasMany(f => f.FormDefinitions)
                .WithOne(fd => fd.Form)
                .HasForeignKey(fd => fd.FormId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(f => f.ActiveDefinition)
                .WithOne()
                .HasForeignKey<Form>(f => f.ActiveDefinitionId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(c => c.ActiveDefinitionId)
                    .IsUnique();
        }
    }
}