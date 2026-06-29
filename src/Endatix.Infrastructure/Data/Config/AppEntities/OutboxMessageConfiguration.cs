using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Outbox.Engine;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>()]
public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    private const int EVENT_TYPE_MAX_LENGTH = 128;
    private const int IDENTIFIER_MAX_LENGTH = 128;

    // Column names + table are pinned to the engine's canonical OutboxSchema so the relay's raw SQL and this
    // EF mapping share one source of truth (a rename is a single compile-time edit on both sides). The names
    // already equal the property names, so this is conformance hardening — no migration. Guarded by the
    // OutboxSchema conformance test.
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable(OutboxSchema.DefaultTable);

        builder.Property(o => o.Id)
            .HasColumnName(OutboxSchema.Id)
            .IsRequired();

        builder.Property(o => o.EventType)
            .HasColumnName(OutboxSchema.EventType)
            .HasMaxLength(EVENT_TYPE_MAX_LENGTH)
            .IsRequired();

        builder.Property(o => o.Payload)
            .HasColumnName(OutboxSchema.Payload)
            .IsRequired();

        builder.Property(o => o.TenantId)
            .HasColumnName(OutboxSchema.TenantId)
            .IsRequired();

        builder.Property(o => o.OccurredAt)
            .HasColumnName(OutboxSchema.OccurredAt)
            .IsRequired();

        builder.Property(o => o.SchemaVersion)
            .HasColumnName(OutboxSchema.SchemaVersion)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasColumnName(OutboxSchema.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(o => o.Attempts)
            .HasColumnName(OutboxSchema.Attempts)
            .IsRequired();

        builder.Property(o => o.TraceId)
            .HasColumnName(OutboxSchema.TraceId)
            .HasMaxLength(IDENTIFIER_MAX_LENGTH)
            .IsRequired(false);

        builder.Property(o => o.LockedUntil)
            .HasColumnName(OutboxSchema.LockedUntil);

        builder.Property(o => o.LockedBy)
            .HasColumnName(OutboxSchema.LockedBy)
            .HasMaxLength(IDENTIFIER_MAX_LENGTH)
            .IsRequired(false);

        builder.Property(o => o.NextAttemptAt)
            .HasColumnName(OutboxSchema.NextAttemptAt);

        builder.Property(o => o.ProcessedAt)
            .HasColumnName(OutboxSchema.ProcessedAt);

        // The relay claim index and the Payload json/jsonb column type are provider-specific —
        // see OutboxMessageConfigurationPostgreSql / OutboxMessageConfigurationSqlServer.
    }
}
