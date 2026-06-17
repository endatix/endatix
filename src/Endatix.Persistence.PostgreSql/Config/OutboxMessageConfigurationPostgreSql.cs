using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Persistence.PostgreSql.Config;

/// <summary>
/// PostgreSQL specific configuration for the <see cref="OutboxMessage"/> entity.
/// </summary>
[ApplyConfigurationFor<AppDbContext>]
public class OutboxMessageConfigurationPostgreSql : IEntityTypeConfiguration<OutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.Property(o => o.Payload)
            .HasColumnType("jsonb");

        // Partial index for the relay's hot claim query (only Pending rows are polled).
        builder.HasIndex(o => new { o.LockedUntil, o.Id })
            .HasDatabaseName("IX_Outbox_Pending")
            .HasFilter($"\"{nameof(OutboxMessage.Status)}\" = {(int)OutboxMessageStatus.Pending}");
    }
}
