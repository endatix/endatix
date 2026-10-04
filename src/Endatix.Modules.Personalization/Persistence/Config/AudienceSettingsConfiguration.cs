using Endatix.Modules.Personalization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Personalization.Persistence.Config;

/// <summary>
/// Provider-agnostic mapping for <see cref="AudienceSettings"/>.
/// </summary>
internal sealed class AudienceSettingsConfiguration : IEntityTypeConfiguration<AudienceSettings>
{
    public const int IdentifierKindMaxLength = 32;

    public void Configure(EntityTypeBuilder<AudienceSettings> builder)
    {
        builder.ToTable("Settings");

        builder.Property(settings => settings.TenantId).IsRequired();

        builder.Property(settings => settings.IdentifierKind)
            .HasMaxLength(IdentifierKindMaxLength)
            .IsRequired();
    }
}
