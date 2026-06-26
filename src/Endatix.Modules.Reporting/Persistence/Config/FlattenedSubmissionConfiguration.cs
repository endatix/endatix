using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Persistence.Config;

[ApplyConfigurationFor<ReportingDbContext>]
internal sealed class FlattenedSubmissionConfiguration : IEntityTypeConfiguration<FlattenedSubmission>
{
    public const string IntegrationStatusColumnName = "IntegrationStatus";

    public void Configure(EntityTypeBuilder<FlattenedSubmission> builder)
    {
        builder.ToTable("FlattenedSubmissions");
        builder.HasKey(x => x.SubmissionId);

        builder.Property(x => x.SubmissionId).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.FormId).IsRequired();
        builder.Property(x => x.DataJson)
            .IsRequired(false);
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.ComplexProperty(x => x.Integration, cp =>
        {
            cp.Property(x => x.Code)
                .HasColumnName(IntegrationStatusColumnName)
                .HasMaxLength(SubmissionIntegrationState.CodeMaxLength)
                .IsRequired();
            cp.Property(x => x.LastAttemptAt)
                .IsRequired(false);
            cp.Property(x => x.ProcessedAt)
                .IsRequired(false);
            cp.Property(x => x.LastError)
                .HasMaxLength(SubmissionIntegrationState.MaxErrorLength)
                .IsRequired(false);
        });

        builder.HasIndex(x => new { x.TenantId, x.FormId, x.SubmissionId });
    }
}
