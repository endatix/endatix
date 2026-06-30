using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Endatix.IntegrationTests;

/// <summary>
/// Proves the full outbox capture path through a real SaveChanges against PostgreSQL: integration events
/// raised on an aggregate become OutboxMessage rows committed in the same transaction as the aggregate
/// (and roll back together when the write fails).
/// </summary>
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

        // Assert — a fresh context proves the row was committed, not merely tracked
        using var verify = CreateContext();
        var outbox = await verify.OutboxMessages.ToListAsync(cancellationToken);

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

        // Act — capture adds the outbox row, then base.SaveChanges throws on the unique violation
        Func<Task> act = async () => await ctx.SaveChangesAsync(cancellationToken);

        // Assert — the failed transaction left NO outbox row: capture is atomic with the aggregate write
        await act.Should().ThrowAsync<DbUpdateException>();
        using var verify = CreateContext();
        (await verify.OutboxMessages.CountAsync(cancellationToken)).Should().Be(0, "the captured outbox row must roll back with the failed aggregate");
        (await verify.Probes.CountAsync(cancellationToken)).Should().Be(1, "only the seeded row should remain");
    }

    [Fact]
    public async Task CreateFormWithDefinitionAsync_FormCreatedPayload_CarriesRealActiveDefinitionId()
    {
        // Exercises the real FormsRepository.CreateFormWithDefinitionAsync so the regression stays tied to
        // its two-save capture ordering (the active definition is added between saves, and form.created must
        // capture a populated activeDefinitionId).
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Database.Checkpoint.ResetAsync(_fixture.Database.ConnectionString, _fixture.Database.Provider, cancellationToken);

        _tenantContext.TenantId.Returns(0L); // bypass tenant query filter so we can read the outbox row back

        long formDefinitionId;
        using (var ctx = CreateContext())
        {
            Tenant tenant = new("acme");
            ctx.Set<Tenant>().Add(tenant);
            await ctx.SaveChangesAsync(cancellationToken);

            FormsRepository repository = new(ctx, new AppUnitOfWork(ctx), new EndatixSpecificationEvaluator([]));
            Form form = new(tenant.Id, "webhook-form", "desc", isEnabled: true);
            FormDefinition formDefinition = new(tenant.Id);

            await repository.CreateFormWithDefinitionAsync(form, formDefinition, cancellationToken);

            formDefinitionId = formDefinition.Id;
            formDefinitionId.Should().BeGreaterThan(0);
        }

        using var verify = CreateContext();
        OutboxMessage created = (await verify.OutboxMessages.ToListAsync(cancellationToken))
            .Single(m => m.EventType == "form.created");

        using var payload = JsonDocument.Parse(created.Payload);
        JsonElement activeDefinitionId = payload.RootElement.GetProperty("activeDefinitionId");
        activeDefinitionId.GetString().Should().Be(
            formDefinitionId.ToString(),
            "the form.created webhook payload must carry the real active definition id, not a stale 0/null");

        _tenantContext.TenantId.Returns(AmbientTenantId);
    }

    private TestAppDbContext CreateContext()
    {
        const string postgresMigrationsAssembly = "Endatix.Persistence.PostgreSql";
        const string appMigrationsNamespace = "Endatix.Persistence.PostgreSql.Migrations.AppEntities";

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(_fixture.Database.ConnectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(postgresMigrationsAssembly);
            npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName);
        });
        ModuleDbContextExtensions.ConfigureProviderScopedMigrations(optionsBuilder, appMigrationsNamespace);

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
