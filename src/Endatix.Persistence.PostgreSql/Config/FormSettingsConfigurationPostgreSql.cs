using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Persistence.PostgreSql.Config;

/// <summary>Stores <see cref="FormSettings"/>' sections as <c>jsonb</c> on PostgreSQL. jsonb reorders keys, so the aggregate compares sections as JSON.</summary>
[ApplyConfigurationFor<AppDbContext>]
internal sealed class FormSettingsConfigurationPostgreSql : IEntityTypeConfiguration<FormSettings>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<FormSettings> builder)
    {
        builder.Property(settings => settings.PdfJson)
            .HasColumnType("jsonb");
    }
}
