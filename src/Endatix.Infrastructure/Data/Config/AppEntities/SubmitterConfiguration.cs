using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>()]
public sealed class SubmitterConfiguration : IEntityTypeConfiguration<Submitter>
{
    private const int AUTH_PROVIDER_MAX_LENGTH = 64;
    private const int EXTERNAL_SUBJECT_MAX_LENGTH = 256;
    private const int DISPLAY_ID_MAX_LENGTH = 256;

    public void Configure(EntityTypeBuilder<Submitter> builder)
    {
        builder.ToTable("Submitters");

        builder.HasKey(submitter => submitter.Id);

        builder.Property(submitter => submitter.AuthProvider)
            .HasMaxLength(AUTH_PROVIDER_MAX_LENGTH)
            .IsRequired();

        builder.Property(submitter => submitter.ExternalSubjectId)
            .HasMaxLength(EXTERNAL_SUBJECT_MAX_LENGTH)
            .IsRequired(false);

        builder.Property(submitter => submitter.DisplayId)
            .HasMaxLength(DISPLAY_ID_MAX_LENGTH)
            .IsRequired(false);

        builder.Property(submitter => submitter.AppUserId)
            .IsRequired(false);

        builder.Property(submitter => submitter.ProfileJson)
            .IsRequired(false);

        builder.Property(submitter => submitter.LastSeenAt)
            .IsRequired();
    }
}
