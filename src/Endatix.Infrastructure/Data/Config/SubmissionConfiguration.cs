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
        }
    }
}