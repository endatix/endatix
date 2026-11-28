using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>]
internal sealed class FormDraftConfiguration : IEntityTypeConfiguration<FormDraft>
{
    // Mirrors Core's internal CanonicalContentHash.LENGTH, checked on both providers.
    internal const int CONTENT_HASH_LENGTH = 64;

    public void Configure(EntityTypeBuilder<FormDraft> builder)
    {
        builder.HasKey(head => head.FormId);
        builder.Property(head => head.FormId).ValueGeneratedNever();
        builder.Property(head => head.Version).IsConcurrencyToken();
        builder.Property(head => head.ContentHash).HasMaxLength(CONTENT_HASH_LENGTH).IsFixedLength().IsUnicode(false);
        builder.HasOne<Form>().WithOne().HasForeignKey<FormDraft>(head => head.FormId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
