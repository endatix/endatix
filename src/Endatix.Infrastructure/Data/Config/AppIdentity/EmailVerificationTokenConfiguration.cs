using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppIdentity;

[ApplyConfigurationFor<AppIdentityDbContext>()]
public class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{

    public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.ToTable("EmailVerificationTokens");

        builder.Property(t => t.Id)
            .IsRequired();

        builder.Property(t => t.UserId)
            .IsRequired();

        builder.Property(t => t.Token)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(t => t.ExpiresAt)
            .IsRequired();

        builder.Property(t => t.IsUsed)
            .IsRequired()
            .HasDefaultValue(false);

        // Create unique index on token value
        builder.HasIndex(t => t.Token)
            .IsUnique();

        // Create index on user ID for quick lookups
        builder.HasIndex(t => t.UserId);

        // Create index on expiry for cleanup operations
        builder.HasIndex(t => t.ExpiresAt);

        // Add foreign key relationship to AppUser
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}