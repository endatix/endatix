using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;

namespace Endatix.Modules.Reporting.Persistence.Config;

[ApplyConfigurationFor<ReportingDbContext>]
internal sealed class FlattenedSubmissionConfiguration : IEntityTypeConfiguration<FlattenedSubmission>
{
    public void Configure(EntityTypeBuilder<FlattenedSubmission> builder)
    {
        builder.ToTable("FlattenedSubmissions");
        builder.HasKey(x => x.SubmissionId);

        builder.Property(x => x.SubmissionId).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.FormId).IsRequired();
        builder.Property(x => x.DataJson)
            .IsRequired();
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.FormId, x.SubmissionId });
    }
}
