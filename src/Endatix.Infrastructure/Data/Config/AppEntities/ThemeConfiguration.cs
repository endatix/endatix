using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>()]
public class ThemeConfiguration : IEntityTypeConfiguration<Theme>
{
    public void Configure(EntityTypeBuilder<Theme> builder)
    {
        builder.ToTable("Themes");

        builder.Property(t => t.Id)
            .IsRequired();

        builder.Property(t => t.Name)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();

        builder.Property(t => t.JsonData)
            .IsRequired();

        builder.HasMany(t => t.Forms)
            .WithOne(f => f.Theme)
            .HasForeignKey(f => f.ThemeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
} 