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
    public async Task HandleAsync_WhenRowExists_MarksDeletedAndExcludesFromQueries()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FlattenedSubmissionRepository repository = CreateRepository(dbContext);
        await repository.GetOrCreateAsync(TenantId, SubmissionId, FormId, cancellationToken);

        Submission submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: FormId,
            FormDefinitionId: 1,
            JsonData: "{}",
            IsComplete: true));
        submission.Id = SubmissionId;
        string payload = JsonSerializer.Serialize(
            new SubmissionDeletedEvent(submission).GetPayload(),
            WireOptions);
        FakeOutboxMessage message = new(
            Id: 1,
            EventType: SubmissionDeletedEvent.EventTypeName,
            Payload: payload,
            TenantId: TenantId);

        SyncSubmissionDeletionOutboxHandler handler = new(repository, NullLogger<SyncSubmissionDeletionOutboxHandler>.Instance);
        await handler.HandleAsync(message, cancellationToken);

        FlattenedSubmission? filtered = await repository.GetBySubmissionIdAsync(
            TenantId,
            SubmissionId,
            cancellationToken);
        filtered.Should().BeNull("deleted rows are excluded by the global IsDeleted query filter");

        FlattenedSubmission persisted = await dbContext.FlattenedSubmissions
            .IgnoreQueryFilters()
            .SingleAsync(row => row.SubmissionId == SubmissionId, cancellationToken);
        persisted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenRowMissing_IsIdempotentNoOp()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FlattenedSubmissionRepository repository = CreateRepository(dbContext);

        Submission submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: FormId,
            FormDefinitionId: 1,
            JsonData: "{}",
            IsComplete: true));
        submission.Id = SubmissionId;
        string payload = JsonSerializer.Serialize(
            new SubmissionDeletedEvent(submission).GetPayload(),
            WireOptions);
        FakeOutboxMessage message = new(
            Id: 2,
            EventType: SubmissionDeletedEvent.EventTypeName,
            Payload: payload,
            TenantId: TenantId);

        SyncSubmissionDeletionOutboxHandler handler = new(repository, NullLogger<SyncSubmissionDeletionOutboxHandler>.Instance);

        Func<Task> act = () => handler.HandleAsync(message, cancellationToken);

        await act.Should().NotThrowAsync();
        (await dbContext.FlattenedSubmissions.CountAsync(cancellationToken)).Should().Be(0);
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

        return new ReportingDbContext(optionsBuilder.Options, new IncrementingIdGenerator(), tenantContext);
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
