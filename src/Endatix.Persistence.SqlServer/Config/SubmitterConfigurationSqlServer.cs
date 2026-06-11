using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Persistence.SqlServer.Config;

[ApplyConfigurationFor<AppDbContext>]
public sealed class SubmitterConfigurationSqlServer : IEntityTypeConfiguration<Submitter>
{
    public void Configure(EntityTypeBuilder<Submitter> builder)
    {
        builder.Property(submitter => submitter.ProfileJson)
            .HasColumnType("json");

        builder.HasIndex(submitter => new
        {
            submitter.TenantId,
            submitter.AuthProvider,
            submitter.AppUserId,
            submitter.ExternalSubjectId
        })
            .HasDatabaseName(Submitter.UniqueConstraints.IdentityPerTenant)
            .IsUnique()
            .HasFilter(
                $"([{nameof(Submitter.AppUserId)}] IS NOT NULL OR [{nameof(Submitter.ExternalSubjectId)}] IS NOT NULL) AND [{nameof(Submitter.IsDeleted)}] = 0");
    }
}
