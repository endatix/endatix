using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Persistence.SqlServer.Config;

/// <summary>Stores <see cref="FormSettings"/>' sections as <c>json</c> on SQL Server.</summary>
[ApplyConfigurationFor<AppDbContext>]
internal sealed class FormSettingsConfigurationSqlServer : IEntityTypeConfiguration<FormSettings>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<FormSettings> builder)
    {
        builder.Property(settings => settings.PdfJson)
            .HasColumnType("json");
    }
}
