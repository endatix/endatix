using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>]
internal sealed class FormDraftConfiguration : IEntityTypeConfiguration<FormDraft>
{
    public void Configure(EntityTypeBuilder<FormDraft> builder)
    {
        builder.HasKey(head => head.FormId);
        builder.Property(head => head.FormId).ValueGeneratedNever();
        builder.Property(head => head.Version).IsConcurrencyToken();
        builder.Property(head => head.ContentHash).HasMaxLength(64);
        builder.HasOne<Form>().WithOne().HasForeignKey<FormDraft>(head => head.FormId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
