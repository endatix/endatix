using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>]
internal sealed class FormDraftOperationConfiguration : IEntityTypeConfiguration<FormDraftOperation>
{
    public void Configure(EntityTypeBuilder<FormDraftOperation> builder)
    {
        builder.HasKey(row => new { row.FormId, row.Version });
        builder.HasOne<FormDraft>().WithMany().HasForeignKey(row => row.FormId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(row => row.ContentHash).HasMaxLength(64);
        builder.OwnsOne(row => row.Actor, actor =>
        {
            actor.Property(value => value.Kind).HasMaxLength(32);
        });
    }
}
