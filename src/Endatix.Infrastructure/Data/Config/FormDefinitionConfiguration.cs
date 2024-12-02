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

            builder.Property(fd => fd.JsonData)
                .IsRequired();

            builder.HasOne(fd => fd.Form)
                .WithMany(f => f.FormDefinitions)
                .HasForeignKey(fd => fd.FormId)
                .OnDelete(DeleteBehavior.NoAction)
                .IsRequired();
                
            builder.HasIndex(fd => fd.FormId);
        }
    }
}