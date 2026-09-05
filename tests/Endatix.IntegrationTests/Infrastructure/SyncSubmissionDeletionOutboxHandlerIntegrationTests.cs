using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.Outbox;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Endatix.Infrastructure.Data;

namespace Endatix.IntegrationTests;

[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class SyncSubmissionDeletionOutboxHandlerIntegrationTests
{
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web);

    private const long TenantId = 1;
    private const long FormId = 100;
    private const long SubmissionId = 500;

    private readonly DbIntegrationFixture _fixture;

    public SyncSubmissionDeletionOutboxHandlerIntegrationTests(DbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HandleAsync_WhenRowExists_HardDeletesOnlyTargetSubmission()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FlattenedSubmissionRepository repository = CreateRepository(dbContext);
        ReportingUnitOfWork unitOfWork = new(dbContext);

        await repository.GetOrCreateAsync(TenantId, SubmissionId, FormId, cancellationToken);
        await repository.GetOrCreateAsync(TenantId, SubmissionId + 1, FormId, cancellationToken);

        SyncSubmissionDeletionOutboxHandler handler = new(
            repository,
            unitOfWork,
            NullLogger<SyncSubmissionDeletionOutboxHandler>.Instance);
        await handler.HandleAsync(CreateMessage(SubmissionId), cancellationToken);

        (await dbContext.FlattenedSubmissions
                .IgnoreQueryFilters()
                .CountAsync(row => row.SubmissionId == SubmissionId, cancellationToken))
            .Should().Be(0);
        (await dbContext.FlattenedSubmissions
                .IgnoreQueryFilters()
                .CountAsync(row => row.SubmissionId == SubmissionId + 1, cancellationToken))
            .Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_WhenRowIsSoftDeleted_StillHardDeletes()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FlattenedSubmissionRepository repository = CreateRepository(dbContext);
        ReportingUnitOfWork unitOfWork = new(dbContext);

        FlattenedSubmission softDeleted = await repository.GetOrCreateAsync(
            TenantId,
            SubmissionId,
            FormId,
            cancellationToken);
        softDeleted.MarkDeleted();
        await repository.SaveAsync(softDeleted, cancellationToken);

        SyncSubmissionDeletionOutboxHandler handler = new(
            repository,
            unitOfWork,
            NullLogger<SyncSubmissionDeletionOutboxHandler>.Instance);
        await handler.HandleAsync(CreateMessage(SubmissionId), cancellationToken);

        (await dbContext.FlattenedSubmissions
                .IgnoreQueryFilters()
                .CountAsync(row => row.SubmissionId == SubmissionId, cancellationToken))
            .Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_WhenRowMissing_IsIdempotentNoOp()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FlattenedSubmissionRepository repository = CreateRepository(dbContext);
        ReportingUnitOfWork unitOfWork = new(dbContext);

        SyncSubmissionDeletionOutboxHandler handler = new(
            repository,
            unitOfWork,
            NullLogger<SyncSubmissionDeletionOutboxHandler>.Instance);

        Func<Task> act = () => handler.HandleAsync(CreateMessage(SubmissionId), cancellationToken);

        await act.Should().NotThrowAsync();
        (await dbContext.FlattenedSubmissions
                .IgnoreQueryFilters()
                .CountAsync(cancellationToken))
            .Should().Be(0);
    }

    private static FakeOutboxMessage CreateMessage(long submissionId)
    {
        Submission submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: FormId,
            FormDefinitionId: 1,
            JsonData: "{}",
            IsComplete: true));
        submission.Id = submissionId;
        string payload = JsonSerializer.Serialize(
            new SubmissionDeletedEvent(submission).GetPayload(),
            WireOptions);

        return new FakeOutboxMessage(
            Id: 1,
            EventType: SubmissionDeletedEvent.EventTypeName,
            Payload: payload,
            TenantId: TenantId);
    }

    private async Task ResetReportingSchemaAsync(CancellationToken cancellationToken)
    {
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
    }

    private ReportingDbContext CreateContext(long tenantId)
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);

        DbContextOptionsBuilder<ReportingDbContext> optionsBuilder =
            ReportingTestSchema.ConfigureOptionsBuilder(_fixture.ConnectionString);

        IncrementingIdGenerator idGenerator = new();

        return new ReportingDbContext(
            optionsBuilder.Options,
            idGenerator,
            tenantContext,
            new EfCoreValueGeneratorFactory(idGenerator));
    }

    private static FlattenedSubmissionRepository CreateRepository(ReportingDbContext dbContext)
    {
        ReportingUnitOfWork unitOfWork = new(dbContext);
        return new FlattenedSubmissionRepository(dbContext, unitOfWork);
    }

    // Mirrors ReportingOutboxTestHelpers.FakeOutboxMessage (Reporting.Tests) and Infrastructure.Tests
    // outbox doubles. Not shared across test projects to avoid a cross-suite test utilities dependency.
    private sealed record FakeOutboxMessage(
        long Id,
        string EventType,
        string Payload,
        long TenantId) : Endatix.Outbox.Engine.IOutboxMessage
    {
        public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;

        public int SchemaVersion => 2;

        public int Attempts => 0;

        public string? TraceId => null;
    }
}
