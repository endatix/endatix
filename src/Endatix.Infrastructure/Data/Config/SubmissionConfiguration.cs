using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;

namespace Endatix.ApplicationCore.Infrastructure.Data.Config
{
    public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
    {
        public void Configure(EntityTypeBuilder<Submission> builder)
        {
            builder.ToTable("Submissions");

            builder.Property(s => s.Id)
                .IsRequired();

            builder.Property(s => s.FormId)
               .IsRequired();

            builder.Property(s => s.JsonData)
                .IsRequired();

            builder.HasOne(s => s.FormDefinition)
                .WithMany(fd => fd.Submissions)
                .HasForeignKey(s => s.FormDefinitionId)
                .OnDelete(DeleteBehavior.NoAction)
               .IsRequired();

            // Index for lookups
            builder.HasIndex(s => s.FormId);
            builder.HasIndex(s => s.FormDefinitionId);
        }
    }
}