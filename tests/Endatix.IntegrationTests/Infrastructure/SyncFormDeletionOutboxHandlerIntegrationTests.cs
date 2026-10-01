using Endatix.Core.Entities;
using Endatix.Core.Events;
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
/// PostgreSQL coverage for form.deleted Reporting cleanup (#902).
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
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
    public async Task HandleAsync_DeletesFormSchemaAndFlattenedRowsForFormOnly()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        ReportingUnitOfWork unitOfWork = new(dbContext);
        FormSchemaRepository schemaRepository = new(dbContext, unitOfWork);
        FlattenedSubmissionRepository flattenedRepository = new(dbContext, unitOfWork);

        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(
            """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1"}]}]}""");

        FormSchema targetSchema = new(
            TenantId,
            FormId,
            FormDefinitionId,
            compiled.FlatteningMapJson,
            compiled.CodebookJson,
            compiled.LocalesJson);
        FormSchema otherSchema = new(
            TenantId,
            OtherFormId,
            FormDefinitionId,
            compiled.FlatteningMapJson,
            compiled.CodebookJson,
            compiled.LocalesJson);
        await schemaRepository.SaveAsync(targetSchema, cancellationToken);
        await schemaRepository.SaveAsync(otherSchema, cancellationToken);

        FlattenedRowSeed rows = new(dbContext);
        await rows.ProcessedAsync(new(TenantId, FormId, SubmissionId: 5001), """{"q1":"a"}""", cancellationToken);
        await rows.ProcessedAsync(new(TenantId, OtherFormId, SubmissionId: 5002), """{"q1":"b"}""", cancellationToken);

        SyncFormDeletionOutboxHandler handler = new(
            schemaRepository,
            flattenedRepository,
            unitOfWork,
            NullLogger<SyncFormDeletionOutboxHandler>.Instance);

        // Act
        await handler.HandleAsync(CreateMessage(), cancellationToken);

        // Assert
        dbContext.ChangeTracker.Clear();
        (await dbContext.FormSchemas
                .IgnoreQueryFilters()
                .CountAsync(row => row.TenantId == TenantId && row.FormId == FormId, cancellationToken))
            .Should().Be(0);
        (await dbContext.FlattenedSubmissions
                .IgnoreQueryFilters()
                .CountAsync(row => row.TenantId == TenantId && row.FormId == FormId, cancellationToken))
            .Should().Be(0);
        (await dbContext.FormSchemas
                .IgnoreQueryFilters()
                .CountAsync(row => row.FormId == OtherFormId, cancellationToken))
            .Should().Be(1);
        (await dbContext.FlattenedSubmissions
                .IgnoreQueryFilters()
                .CountAsync(row => row.FormId == OtherFormId, cancellationToken))
            .Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_WhenNoReportingRows_SucceedsAsNoOp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        ReportingUnitOfWork unitOfWork = new(dbContext);
        SyncFormDeletionOutboxHandler handler = new(
            new FormSchemaRepository(dbContext, unitOfWork),
            new FlattenedSubmissionRepository(dbContext, unitOfWork),
            unitOfWork,
            NullLogger<SyncFormDeletionOutboxHandler>.Instance);

        Func<Task> act = () => handler.HandleAsync(CreateMessage(), cancellationToken);

        await act.Should().NotThrowAsync();
    }

    private async Task ResetReportingSchemaAsync(CancellationToken cancellationToken)
    {
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
    }

    private ReportingDbContext CreateContext(long tenantId)
    {
        IntegrationTenantContext tenantContext = new(tenantId);

        var optionsBuilder =
            ReportingTestSchema.ConfigureOptionsBuilder(_fixture.ConnectionString);

        return new ReportingDbContext(optionsBuilder.Options, tenantContext);
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
