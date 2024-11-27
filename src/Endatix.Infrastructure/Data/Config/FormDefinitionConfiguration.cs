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

            builder.HasIndex(fd => fd.FormId);
        }
    }
}