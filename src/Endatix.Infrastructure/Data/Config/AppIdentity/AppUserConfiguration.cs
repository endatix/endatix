using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppIdentity;

[ApplyConfigurationFor<AppIdentityDbContext>()]
public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    private const int AUTH_PROVIDER_MAX_LENGTH = 64;
    private const int EXTERNAL_SUBJECT_ID_MAX_LENGTH = 256;
    private const int DISPLAY_NAME_MAX_LENGTH = 256;

    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(user => user.AuthProvider)
            .HasMaxLength(AUTH_PROVIDER_MAX_LENGTH)
            .HasDefaultValue(AuthProviders.Endatix)
            .IsRequired();

        builder.Property(user => user.ExternalSubjectId)
            .HasMaxLength(EXTERNAL_SUBJECT_ID_MAX_LENGTH)
            .IsRequired(false);

        builder.Property(user => user.DisplayName)
            .HasMaxLength(DISPLAY_NAME_MAX_LENGTH)
            .IsRequired(false);

        builder.Property(user => user.LastLoginAt)
            .IsRequired(false);

        builder.Property(user => user.ExternalRolesJson)
            .IsRequired(false);

        builder.Ignore(user => user.IsExternal);

        builder.HasIndex(user => new
        {
            user.TenantId,
            user.AuthProvider,
            user.ExternalSubjectId
        })
            .IsUnique()
            .HasDatabaseName(AppUser.UniqueConstraints.ExternalIdentityPerTenant)
            .HasFilter("\"ExternalSubjectId\" IS NOT NULL");

        builder.HasIndex(user => new
        {
            user.TenantId,
            user.AuthProvider
        });

        builder.HasIndex(user => new
        {
            user.TenantId,
            user.NormalizedEmail
        })
            .IsUnique()
            .HasDatabaseName(AppUser.UniqueConstraints.EmailPerTenant)
            .HasFilter("\"NormalizedEmail\" IS NOT NULL");
    }
}
