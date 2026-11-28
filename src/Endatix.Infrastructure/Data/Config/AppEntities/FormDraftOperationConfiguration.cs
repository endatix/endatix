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
        // Form.Delete is a soft delete and retains history. For physical removal, ExecuteDelete the
        // parent in a dedicated scope: the database cascades even when prior saves tracked history.
        // Remove(history) and tracked client cascades are deliberately refused by InsertOnlyInterceptor.
        builder.HasOne<FormDraft>().WithMany().HasForeignKey(row => row.FormId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(row => row.ContentHash).HasMaxLength(FormDraftConfiguration.CONTENT_HASH_LENGTH).IsFixedLength().IsUnicode(false);
        builder.Property(row => row.Kind).HasMaxLength(FormDraftOperationKinds.MAX_LENGTH);
        builder.Property(row => row.Reason).HasMaxLength(DraftSnapshotReasons.MAX_LENGTH);
        builder.Property(row => row.Channel).HasMaxLength(DraftChannels.MAX_LENGTH);
        builder.Property(row => row.EditorSessionId).HasMaxLength(FormDraftOperation.EDITOR_SESSION_ID_MAX_LENGTH);
        builder.Property(row => row.Label).HasMaxLength(FormDraftOperation.LABEL_MAX_LENGTH);
        builder.OwnsOne(row => row.Actor, actor =>
        {
            actor.Property(value => value.Kind).HasMaxLength(32);
        });
    }
}
