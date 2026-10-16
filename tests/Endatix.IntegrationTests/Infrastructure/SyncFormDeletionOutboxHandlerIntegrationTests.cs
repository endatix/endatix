using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.Outbox;
using Endatix.Modules.Reporting.Persistence;
using Endatix.Outbox.Engine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Endatix.IntegrationTests;

/// <summary>
/// Database coverage for form.deleted Reporting cleanup (#902).
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class SyncFormDeletionOutboxHandlerIntegrationTests
{
    private const long TenantId = 61;
    private const long FormId = 700;
    private const long OtherFormId = 701;
    private const long FormDefinitionId = 800;

    private readonly DbIntegrationFixture _fixture;

    public SyncFormDeletionOutboxHandlerIntegrationTests(DbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HandleAsync_DeletesFormSchemaFlattenedRowsAndViewsForFormOnly()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        ReportingUnitOfWork unitOfWork = new(dbContext);
        FormSchemaRepository schemaRepository = new(dbContext, unitOfWork, ReportingTestSchema.TransactionLockFor(dbContext));
        FlattenedSubmissionRepository flattenedRepository = new(dbContext, unitOfWork, new DateTimeProvider());
        SubmissionViewRepository viewRepository = new(dbContext, new UniqueConstraintViolationChecker());

        await SeedFormRowsAsync(dbContext, new(TenantId, FormId, SubmissionId: 5001), cancellationToken);
        await SeedFormRowsAsync(dbContext, new(TenantId, OtherFormId, SubmissionId: 5002), cancellationToken);

        SyncFormDeletionOutboxHandler handler = new(
            schemaRepository,
            flattenedRepository,
            viewRepository,
            unitOfWork,
            NullLogger<SyncFormDeletionOutboxHandler>.Instance);

        // Act
        await handler.HandleAsync(CreateMessage(), cancellationToken);

        // Assert
        dbContext.ChangeTracker.Clear();
        (await RowCountsAsync(dbContext, FormId, cancellationToken)).Should().Be(new FormRowCounts(0, 0, 0));
        (await RowCountsAsync(dbContext, OtherFormId, cancellationToken)).Should().Be(new FormRowCounts(1, 1, 1));
    }

    [Fact]
    public async Task HandleAsync_WhenNoReportingRows_SucceedsAsNoOp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        ReportingUnitOfWork unitOfWork = new(dbContext);
        SyncFormDeletionOutboxHandler handler = new(
            new FormSchemaRepository(dbContext, unitOfWork, ReportingTestSchema.TransactionLockFor(dbContext)),
            new FlattenedSubmissionRepository(dbContext, unitOfWork, new DateTimeProvider()),
            new SubmissionViewRepository(dbContext, new UniqueConstraintViolationChecker()),
            unitOfWork,
            NullLogger<SyncFormDeletionOutboxHandler>.Instance);

        Func<Task> act = () => handler.HandleAsync(CreateMessage(), cancellationToken);

        await act.Should().NotThrowAsync();
    }

    // A compiled schema, one processed flattened row and a saved grid view: every form-scoped Reporting row.
    private static async Task SeedFormRowsAsync(
        ReportingDbContextBase dbContext,
        FlattenedSubmissionKey submission,
        CancellationToken cancellationToken)
    {
        var formId = submission.FormId;
        var compiled = new FormSchemaCompiler().CompilePersisted(
            """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1"}]}]}""");
        dbContext.FormSchemas.Add(new FormSchema(
            TenantId,
            formId,
            FormDefinitionId,
            compiled.FlatteningMapJson,
            compiled.CodebookJson,
            compiled.LocalesJson));
        dbContext.SubmissionViews.Add(SubmissionView.CreateShared(
            new SubmissionViewCreateArgs(TenantId, formId, SubmissionViewSettings.DefaultJson, ModifiedBy: null)));
        await dbContext.SaveChangesAsync(cancellationToken);

        FlattenedRowSeed rows = new(dbContext);
        await rows.ProcessedAsync(submission, """{"q1":"a"}""", cancellationToken);
    }

    private static async Task<FormRowCounts> RowCountsAsync(
        ReportingDbContextBase dbContext,
        long formId,
        CancellationToken cancellationToken) =>
        new(
            await dbContext.FormSchemas.IgnoreQueryFilters().CountAsync(row => row.FormId == formId, cancellationToken),
            await dbContext.FlattenedSubmissions.IgnoreQueryFilters().CountAsync(row => row.FormId == formId, cancellationToken),
            await dbContext.SubmissionViews.IgnoreQueryFilters().CountAsync(row => row.FormId == formId, cancellationToken));

    private sealed record FormRowCounts(int Schemas, int Flattened, int Views);

    private async Task ResetReportingSchemaAsync(CancellationToken cancellationToken)
    {
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
    }

    private ReportingDbContextBase CreateContext(long tenantId)
    {
        IntegrationTenantContext tenantContext = new(tenantId);

        return ReportingTestSchema.CreateContext(_fixture.ConnectionString, _fixture.Provider, tenantContext);
    }

    private static IOutboxMessage CreateMessage()
    {
        var form = Form.Create(new FormCreateArgs(TenantId: TenantId, Name: "to-delete"));
        form.Id = FormId;
        var payloadObject = new FormDeletedEvent(form).GetPayload();
        var payload = JsonSerializer.Serialize(
            payloadObject,
            payloadObject.GetType(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return new FakeOutboxMessage(
            Id: 1,
            EventType: FormDeletedEvent.EventTypeName,
            Payload: payload,
            TenantId: TenantId);
    }

    private sealed record FakeOutboxMessage(
        long Id,
        string EventType,
        string Payload,
        long TenantId) : IOutboxMessage
    {
        public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;

        public int SchemaVersion => 2;

        public int Attempts => 0;

        public string? TraceId => null;
    }
}
