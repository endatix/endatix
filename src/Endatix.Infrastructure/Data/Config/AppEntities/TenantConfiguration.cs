using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>()]
public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public const string UniqueSlugIndexName = "IX_Tenants_Slug";

    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.Property(f => f.Id)
            .IsRequired();

        builder.Property(t => t.Name)
            .HasMaxLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .IsRequired();

        builder.Property(t => t.Slug)
            .HasMaxLength(DataSchemaConstants.MAX_SLUG_LENGTH)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(DataSchemaConstants.MAX_DESCRIPTION_LENGTH)
            .IsRequired(false);

        builder.HasIndex(t => t.Slug)
            .IsUnique()
            .HasDatabaseName(UniqueSlugIndexName);

        builder.HasMany(t => t.Forms)
            .WithOne(f => f.Tenant)
            .HasForeignKey(f => f.TenantId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.FormDefinitions)
            .WithOne(fd => fd.Tenant)
            .HasForeignKey(fd => fd.TenantId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Submissions)
            .WithOne(s => s.Tenant)
            .HasForeignKey(s => s.TenantId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
