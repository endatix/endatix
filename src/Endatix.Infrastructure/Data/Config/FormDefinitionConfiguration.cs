using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Data.Config
{
    public class FormDefinitionConfiguration : IEntityTypeConfiguration<FormDefinition> 
    {
        public void Configure(EntityTypeBuilder<FormDefinition> builder)
        {
            builder.ToTable("FormDefinitions");

            builder.Property(fd => fd.Id)
                .IsRequired();

            builder.HasMany(fd => fd.Submissions)
                .WithOne(s => s.FormDefinition)
                .HasForeignKey(s => s.FormDefinitionId)
                .OnDelete(DeleteBehavior.NoAction);

            // This will ensure only one definition per form can be active
            builder.HasIndex(fd => new { fd.FormId, fd.IsActive })
                .HasFilter("IsActive = 1")
                .IsUnique();
        }
    }
}