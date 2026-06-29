using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Identity.Authentication;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

/// <summary>
/// Proves the full Phase 2 capture path through a real <see cref="DbContext.SaveChanges()"/> against a
/// SQLite database: integration events raised on an aggregate become OutboxMessage rows committed in the
/// SAME transaction as the aggregate (and roll back together when the write fails).
/// </summary>
public sealed class OutboxCaptureIntegrationTests : IDisposable
{
    private const long AmbientTenantId = 42;
    private readonly SqliteConnection _connection;
    private readonly ITenantContext _tenantContext;
    // One generator for the fixture: contexts share the DB connection, so Ids must be unique across them.
    private readonly IncrementingIdGenerator _idGenerator = new();

    public OutboxCaptureIntegrationTests()
    {
        // A shared in-memory connection kept open for the test, so the schema survives across contexts.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(AmbientTenantId);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task SaveChanges_WhenAggregateRaisesIntegrationEvent_CommitsOneOutboxRow()
    {
        // Arrange
        using (var ctx = CreateContext())
        {
            await ctx.Database.EnsureCreatedAsync();
            var probe = new OutboxCaptureProbe { Name = "alpha" };
            probe.RaiseCreated(formId: 555);
            ctx.Probes.Add(probe);

            // Act
            await ctx.SaveChangesAsync();
        }

        // Assert — a fresh context proves the row was committed, not merely tracked
        using var verify = CreateContext();
        var outbox = await verify.OutboxMessages.ToListAsync();

        outbox.Should().ContainSingle();
        outbox[0].EventType.Should().Be("probe.created");
        // The probe is not tenant-owned → app-level event (DEFAULT_TENANT_ID), regardless of ambient context.
        outbox[0].TenantId.Should().Be(AuthConstants.DEFAULT_TENANT_ID);
        outbox[0].Status.Should().Be(OutboxMessageStatus.Pending);
        outbox[0].Attempts.Should().Be(0);
        outbox[0].Id.Should().BeGreaterThan(0, "ProcessEntities stamps the outbox row's Id explicitly");

        using var payload = JsonDocument.Parse(outbox[0].Payload);
        // Long IDs are serialized as strings on the wire (LongToStringConverter).
        payload.RootElement.GetProperty("formId").GetString().Should().Be("555");
    }

    [Fact]
    public async Task SaveChanges_WhenTheAggregateWriteFails_RollsBackTheOutboxRowToo()
    {
        // Arrange — seed a row so a duplicate Name violates the unique index on the next write
        using (var seed = CreateContext())
        {
            await seed.Database.EnsureCreatedAsync();
            seed.Probes.Add(new OutboxCaptureProbe { Name = "dup" });
            await seed.SaveChangesAsync();
        }

        using var ctx = CreateContext();
        var probe = new OutboxCaptureProbe { Name = "dup" }; // duplicate → the INSERT will fail
        probe.RaiseCreated(formId: 1);
        ctx.Probes.Add(probe);

        // Act — capture adds the outbox row, then base.SaveChanges throws on the unique violation
        var act = async () => await ctx.SaveChangesAsync();

        // Assert — the failed transaction left NO outbox row: capture is atomic with the aggregate write
        await act.Should().ThrowAsync<DbUpdateException>();

        using var verify = CreateContext();
        (await verify.OutboxMessages.CountAsync()).Should().Be(0, "the captured row must roll back with the failed aggregate");
        (await verify.Probes.CountAsync()).Should().Be(1, "only the seeded row should remain");
    }

    private TestAppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new TestAppDbContext(
            options,
            _idGenerator,
            _tenantContext,
            new EfCoreValueGeneratorFactory(_idGenerator),
            new OutboxIntegrationEventDispatcher());
    }

    private sealed class IncrementingIdGenerator : IIdGenerator<long>
    {
        private long _current = 1_000;
        public long CreateId() => Interlocked.Increment(ref _current);
    }
}

/// <summary>Test context that maps a probe entity alongside the real Endatix model.</summary>
internal sealed class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(
        DbContextOptions<AppDbContext> options,
        IIdGenerator<long> idGenerator,
        ITenantContext tenantContext,
        EfCoreValueGeneratorFactory valueGeneratorFactory,
        OutboxIntegrationEventDispatcher outboxDispatcher)
        : base(options, idGenerator, tenantContext, valueGeneratorFactory, outboxDispatcher)
    {
    }

    public DbSet<OutboxCaptureProbe> Probes => Set<OutboxCaptureProbe>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<OutboxCaptureProbe>(entity =>
        {
            entity.ToTable("OutboxCaptureProbes");
            entity.HasKey(probe => probe.Id);
            entity.Property(probe => probe.Name).IsRequired();
            entity.HasIndex(probe => probe.Name).IsUnique();
        });
    }
}

/// <summary>A minimal aggregate (not tenant-owned) that can raise an integration event for the test.</summary>
internal sealed class OutboxCaptureProbe : BaseEntity
{
    public string Name { get; set; } = null!;

    public void RaiseCreated(long formId) => RegisterDomainEvent(new ProbeCreatedEvent(formId));
}

internal sealed class ProbeCreatedEvent(long formId) : DomainEventBase, IIntegrationEvent
{
    private readonly ProbeCreatedPayload _payload = new(formId);

    public string EventType => "probe.created";

    public object GetPayload() => _payload;
}

internal sealed record ProbeCreatedPayload(long FormId);
