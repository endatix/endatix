using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Outbox.Engine;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

/// <summary>
/// Guards engine↔host agreement: the EF mapping of <see cref="OutboxMessage"/> must conform to the engine's
/// canonical <see cref="OutboxSchema"/> (table + column names) and <see cref="OutboxStatus"/> (int values),
/// because the relay's raw SQL is built from those constants. A drift here would only surface at runtime
/// against a real DB; this test catches it at build/test time.
/// </summary>
public sealed class OutboxSchemaConformanceTests : IDisposable
{
    private readonly AppDbContext _context;

    public OutboxSchemaConformanceTests()
    {
        var idGenerator = Substitute.For<IIdGenerator<long>>();
        var tenantContext = Substitute.For<ITenantContext>();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new AppDbContext(
            options,
            idGenerator,
            tenantContext,
            new EfCoreValueGeneratorFactory(idGenerator),
            new OutboxIntegrationEventDispatcher());
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void OutboxMessage_maps_to_the_canonical_table_and_columns()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(OutboxMessage))!;

        // Act
        var store = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table)!.Value;
        var columns = entityType.GetProperties().Select(p => p.GetColumnName(store)!).ToHashSet();

        // Assert
        entityType.GetTableName().Should().Be(OutboxSchema.DefaultTable);
        columns.Should().Contain(new[]
        {
            OutboxSchema.Id, OutboxSchema.EventType, OutboxSchema.Payload, OutboxSchema.TenantId,
            OutboxSchema.OccurredAt, OutboxSchema.SchemaVersion, OutboxSchema.Status, OutboxSchema.Attempts,
            OutboxSchema.TraceId, OutboxSchema.LockedUntil, OutboxSchema.LockedBy, OutboxSchema.NextAttemptAt,
            OutboxSchema.ProcessedAt,
        });
    }

    [Fact]
    public void Status_is_stored_as_int()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(OutboxMessage))!;

        // Act
        var status = entityType.FindProperty(nameof(OutboxMessage.Status))!;

        // Assert
        status.GetProviderClrType().Should().Be(typeof(int));
    }

    [Fact]
    public void OutboxMessageStatus_values_match_the_engine_OutboxStatus()
    {
        // Assert — the host enum's int values must match the engine's, since the claim SQL filters on them.
        ((int)OutboxMessageStatus.Pending).Should().Be((int)OutboxStatus.Pending);
        ((int)OutboxMessageStatus.Sent).Should().Be((int)OutboxStatus.Sent);
        ((int)OutboxMessageStatus.Failed).Should().Be((int)OutboxStatus.Failed);
    }
}
