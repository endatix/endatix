using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Persistence.PostgreSql.Config;

[ApplyConfigurationFor<AppDbContext>]
public sealed class SubmitterConfigurationPostgreSql : IEntityTypeConfiguration<Submitter>
{
    public void Configure(EntityTypeBuilder<Submitter> builder)
    {
        builder.Property(submitter => submitter.ProfileJson)
            .HasColumnType("jsonb");

        builder.HasIndex(submitter => new
        {
            submitter.TenantId,
            submitter.AuthProvider,
            submitter.AppUserId
        })
            .HasDatabaseName(Submitter.UniqueConstraints.AppUserPerTenant)
            .IsUnique()
            .HasFilter($"\"{nameof(Submitter.AppUserId)}\" IS NOT NULL AND \"{nameof(Submitter.IsDeleted)}\" = false");

        builder.HasIndex(submitter => new
        {
            submitter.TenantId,
            submitter.AuthProvider,
            submitter.ExternalSubjectId
        })
            .HasDatabaseName(Submitter.UniqueConstraints.ExternalSubjectPerTenant)
            .IsUnique()
            .HasFilter($"\"{nameof(Submitter.ExternalSubjectId)}\" IS NOT NULL AND \"{nameof(Submitter.IsDeleted)}\" = false");
    }
}
