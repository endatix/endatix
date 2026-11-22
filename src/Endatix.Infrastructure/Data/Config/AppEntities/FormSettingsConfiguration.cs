using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

/// <summary>
/// Maps <see cref="FormSettings"/>: the form id as key and foreign key, and <c>Version</c> as the concurrency token. The
/// form has no navigation to its settings, so loading a form never loads them.
/// </summary>
[ApplyConfigurationFor<AppDbContext>()]
internal sealed class FormSettingsConfiguration : IEntityTypeConfiguration<FormSettings>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<FormSettings> builder)
    {
        builder.ToTable("FormSettings");

        // FormId is the primary key (1-to-1 with Form)
        builder.HasKey(settings => settings.FormId);

        builder.Property(settings => settings.FormId)
            .ValueGeneratedNever();

        builder.Property(settings => settings.Version)
            .IsConcurrencyToken();

        builder.HasOne<Form>()
            .WithOne()
            .HasForeignKey<FormSettings>(settings => settings.FormId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
