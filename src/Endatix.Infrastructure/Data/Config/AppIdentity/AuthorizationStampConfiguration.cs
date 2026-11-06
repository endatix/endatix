using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppIdentity;

/// <summary>Maps the single <see cref="AuthorizationStamp"/> row and seeds it.</summary>
[ApplyConfigurationFor<AppIdentityDbContext>()]
public class AuthorizationStampConfiguration : IEntityTypeConfiguration<AuthorizationStamp>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuthorizationStamp> builder)
    {
        builder.ToTable("AuthorizationStamps");

        builder.HasKey(stamp => stamp.Id);

        builder.Property(stamp => stamp.Id)
            .ValueGeneratedNever();

        builder.Property(stamp => stamp.Value)
            .IsRequired();

        builder.HasData(new { Id = AuthorizationStamp.ROW_ID, Value = AuthorizationStamp.Initial });
    }
}
