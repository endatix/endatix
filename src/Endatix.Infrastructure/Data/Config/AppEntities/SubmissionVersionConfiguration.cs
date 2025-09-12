using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data.Config;
using Endatix.Infrastructure.Data;

namespace Endatix.ApplicationCore.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>()]
public class SubmissionVersionConfiguration : IEntityTypeConfiguration<SubmissionVersion>
{
    public void Configure(EntityTypeBuilder<SubmissionVersion> builder)
    {
        builder.ToTable("SubmissionVersions");

        builder.Property(sv => sv.Id)
            .IsRequired();

        builder.Property(sv => sv.SubmissionId)
            .IsRequired();

        builder.Property(sv => sv.JsonData)
            .IsRequired();

        builder.HasOne(sv => sv.Submission)
            .WithMany()
            .HasForeignKey(sv => sv.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasIndex(sv => sv.SubmissionId);
    }
}
