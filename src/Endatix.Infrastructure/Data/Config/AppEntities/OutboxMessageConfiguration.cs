using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>()]
public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    private const int EVENT_TYPE_MAX_LENGTH = 128;
    private const int IDENTIFIER_MAX_LENGTH = 128;

    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.Property(o => o.Id)
            .IsRequired();

        builder.Property(o => o.EventType)
            .HasMaxLength(EVENT_TYPE_MAX_LENGTH)
            .IsRequired();

        builder.Property(o => o.Payload)
            .IsRequired();

        builder.Property(o => o.TenantId)
            .IsRequired();

        builder.Property(o => o.OccurredAt)
            .IsRequired();

        builder.Property(o => o.SchemaVersion)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(o => o.Attempts)
            .IsRequired();

        builder.Property(o => o.TraceId)
            .HasMaxLength(IDENTIFIER_MAX_LENGTH)
            .IsRequired(false);

        builder.Property(o => o.LockedBy)
            .HasMaxLength(IDENTIFIER_MAX_LENGTH)
            .IsRequired(false);

        // The relay claim index and the Payload json/jsonb column type are provider-specific —
        // see OutboxMessageConfigurationPostgreSql / OutboxMessageConfigurationSqlServer.
    }
}
