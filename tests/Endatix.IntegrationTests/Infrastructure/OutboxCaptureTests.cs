using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Identity.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class OutboxCaptureTests
{
    private const long AmbientTenantId = 42;
    private readonly EndatixIntegrationWebHostFixture _fixture;
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IncrementingIdGenerator _idGenerator = new();

    public OutboxCaptureTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
        _tenantContext.TenantId.Returns(AmbientTenantId);
    }

    [Fact]
    public async Task SaveChanges_WhenAggregateRaisesIntegrationEvent_CommitsOneOutboxRow()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Database.Checkpoint.ResetAsync(_fixture.Database.ConnectionString, _fixture.Database.Provider, cancellationToken);

        using (var ctx = CreateContext())
        {
            await EnsureProbeTableAsync(ctx, cancellationToken);
            OutboxCaptureProbe probe = new() { Name = "alpha" };
            probe.RaiseCreated(formId: 555);
            ctx.Probes.Add(probe);

            // Act
            await ctx.SaveChangesAsync(cancellationToken);
        }

        // Assert
        using var verify = CreateContext();
        var outbox = await verify.OutboxMessages.ToListAsync(cancellationToken);

        outbox.Should().ContainSingle();
        outbox[0].EventType.Should().Be("probe.created");
        outbox[0].TenantId.Should().Be(AuthConstants.DEFAULT_TENANT_ID);
        outbox[0].Status.Should().Be(OutboxMessageStatus.Pending);
        outbox[0].Attempts.Should().Be(0);
        outbox[0].Id.Should().BeGreaterThan(0, "ProcessEntities stamps the outbox row's Id explicitly");

        using var payload = JsonDocument.Parse(outbox[0].Payload);
        payload.RootElement.GetProperty("formId").GetString().Should().Be("555");
    }

    [Fact]
    public async Task SaveChanges_WhenTheAggregateWriteFails_RollsBackTheOutboxRowToo()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Database.Checkpoint.ResetAsync(_fixture.Database.ConnectionString, _fixture.Database.Provider, cancellationToken);

        using (var seed = CreateContext())
        {
            await EnsureProbeTableAsync(seed, cancellationToken);
            seed.Probes.Add(new OutboxCaptureProbe { Name = "dup" });
            await seed.SaveChangesAsync(cancellationToken);
        }

        using var ctx = CreateContext();
        OutboxCaptureProbe probe = new() { Name = "dup" };
        probe.RaiseCreated(formId: 1);
        ctx.Probes.Add(probe);

        // Act
        Func<Task> act = async () => await ctx.SaveChangesAsync(cancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        using var verify = CreateContext();
        (await verify.OutboxMessages.CountAsync(cancellationToken)).Should().Be(0, "the captured outbox row must roll back with the failed aggregate");
        (await verify.Probes.CountAsync(cancellationToken)).Should().Be(1, "only the seeded row should remain");
    }

    private TestAppDbContext CreateContext()
    {
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(_fixture.Database.ConnectionString);
        ModuleDbContextExtensions.ConfigureProviderScopedMigrations(
            optionsBuilder,
            "Endatix.Persistence.PostgreSql.Migrations.AppEntities");

        return new TestAppDbContext(
            optionsBuilder.Options,
            _idGenerator,
            _tenantContext,
            new EfCoreValueGeneratorFactory(_idGenerator),
            new OutboxIntegrationEventDispatcher());
    }

    private static async Task EnsureProbeTableAsync(TestAppDbContext context, CancellationToken cancellationToken)
    {
        // Shared DB may already exist from WebHost migrations; EnsureCreated is a no-op then.
        await context.Database.ExecuteSqlRawAsync(
            """
            DROP TABLE IF EXISTS "OutboxCaptureProbes";
            CREATE TABLE "OutboxCaptureProbes" (
                "Id" bigint NOT NULL PRIMARY KEY,
                "Name" text NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "ModifiedAt" timestamp with time zone,
                "DeletedAt" timestamp with time zone,
                "IsDeleted" boolean NOT NULL DEFAULT FALSE,
                CONSTRAINT "UX_OutboxCaptureProbes_Name" UNIQUE ("Name")
            );
            """,
            cancellationToken);
    }

    private sealed class IncrementingIdGenerator : IIdGenerator<long>
    {
        private long _current = 1_000;
        public long CreateId() => Interlocked.Increment(ref _current);
    }
}

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

        builder.Entity<OutboxMessage>()
            .Property(message => message.Payload)
            .HasColumnType("jsonb");
    }
}

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
