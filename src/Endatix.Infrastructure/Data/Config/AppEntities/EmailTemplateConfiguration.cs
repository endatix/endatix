using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>()]
public class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("EmailTemplates");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Subject)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.HtmlContent)
            .IsRequired();

        builder.Property(e => e.PlainTextContent)
            .IsRequired();

        builder.Property(e => e.FromAddress)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(e => e.Name)
            .IsUnique();
    }
}