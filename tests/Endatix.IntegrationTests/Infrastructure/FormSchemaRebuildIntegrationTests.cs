using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Repositories;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.IntegrationTests;

/// <summary>
/// Rebuilds of one form's schema against PostgreSQL.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class FormSchemaRebuildIntegrationTests(DbIntegrationFixture fixture)
{
    private const long TenantId = 52;

    // Long enough for both rebuilds to read the schema before either saves, when nothing keeps them apart.
    private static readonly TimeSpan BothReadTimeout = TimeSpan.FromSeconds(2);

    private const string BaseDefinition = """
        { "pages": [ { "elements": [ { "type": "text", "name": "q0", "title": "Base" } ] } ] }
        """;

    private const string AddsQuestionA = """
        { "pages": [ { "elements": [
          { "type": "text", "name": "q0", "title": "Base" },
          { "type": "text", "name": "qa", "title": "Added by A" }
        ] } ] }
        """;

    private const string AddsQuestionB = """
        { "pages": [ { "elements": [
          { "type": "text", "name": "q0", "title": "Base" },
          { "type": "text", "name": "qb", "title": "Added by B" }
        ] } ] }
        """;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Concurrent_rebuilds_of_one_form_keep_every_question_either_adds(bool schemaAlreadyCompiled)
    {
        // Arrange
        var form = await SeedFormAsync([BaseDefinition, AddsQuestionA, AddsQuestionB]);
        if (schemaAlreadyCompiled)
        {
            await CompileAsync(form.FormId, form.DefinitionIds[0]);
        }

        Rendezvous bothRead = new(parties: 2);

        // Act
        await Task.WhenAll(
            CompileHeldAfterReadAsync(form.FormId, form.DefinitionIds[1], bothRead),
            CompileHeldAfterReadAsync(form.FormId, form.DefinitionIds[2], bothRead));

        // Assert
        var schema = await ReadSchemaAsync(form.FormId);
        FormSchemaFlatteningMap.FromJson(schema.FlatteningMap).Columns
            .Select(column => column.Key)
            .Should().Contain(["q0", "qa", "qb"]);
        schema.FormDefinitionRevision.Should().Be(form.DefinitionIds[2]);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private Task CompileAsync(long formId, long formDefinitionId) =>
        CompileWithAsync(formId, formDefinitionId, schemas => schemas);

    private Task CompileHeldAfterReadAsync(long formId, long formDefinitionId, Rendezvous rendezvous) =>
        CompileWithAsync(formId, formDefinitionId, schemas => new WaitAfterLockedRead(schemas, rendezvous));

    private async Task CompileWithAsync(
        long formId,
        long formDefinitionId,
        Func<IFormSchemaRepository, IFormSchemaRepository> wrapSchemas)
    {
        await using var appDb = CreateAppDbContext();
        await using var reportingDb = CreateReportingDbContext();
        var schemas = wrapSchemas(new FormSchemaRepository(reportingDb, new ReportingUnitOfWork(reportingDb)));

        await CreateProcessor(appDb, reportingDb, schemas)
            .ProcessAsync(TenantId, formId, formDefinitionId, cancellationToken: Cancellation);
    }

    private static FormSchemaProcessor CreateProcessor(
        AppDbContext appDb,
        ReportingDbContext reportingDb,
        IFormSchemaRepository schemas)
    {
        ReportingUnitOfWork unitOfWork = new(reportingDb);
        return new FormSchemaProcessor(
            CreateFormsRepository(appDb),
            schemas,
            new FlattenedSubmissionRepository(reportingDb, unitOfWork),
            unitOfWork,
            appDb,
            new FormSchemaCompiler(),
            NullLogger<FormSchemaProcessor>.Instance);
    }

    private static FormsRepository CreateFormsRepository(AppDbContext appDb) =>
        new(appDb, new AppUnitOfWork(appDb), new EndatixSpecificationEvaluator([]));

    // One real submission on the oldest definition, so every compile merges.
    private async Task<SeededForm> SeedFormAsync(string[] definitionsOldestFirst)
    {
        await fixture.Checkpoint.ResetAsync(fixture.ConnectionString, fixture.Provider, Cancellation);
        await ReportingTestSchema.EnsureMigratedAsync(fixture.ConnectionString, fixture.Provider, Cancellation);

        await using var appDb = CreateAppDbContext();
        appDb.Set<Tenant>().Add(new Tenant("form-schema-rebuild-tenant", "tnntfsrb") { Id = TenantId });
        await appDb.SaveChangesAsync(Cancellation);

        // Saved before its definitions, which avoids the Form <-> ActiveDefinition circular insert.
        var form = Form.Create(new FormCreateArgs(TenantId: TenantId, Name: "Rebuilt form"));
        appDb.Forms.Add(form);
        await appDb.SaveChangesAsync(Cancellation);

        List<long> definitionIds = [];
        foreach (var json in definitionsOldestFirst)
        {
            FormDefinition definition = new(TenantId, isDraft: false, jsonData: json);
            form.AddFormDefinition(definition);
            appDb.Set<FormDefinition>().Add(definition);
            await appDb.SaveChangesAsync(Cancellation);
            definitionIds.Add(definition.Id);
        }

        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: form.Id,
            FormDefinitionId: definitionIds[0],
            JsonData: """{"q0":"first answer"}""",
            IsComplete: true));
        appDb.Submissions.Add(submission);
        await appDb.SaveChangesAsync(Cancellation);
        return new SeededForm(form.Id, definitionIds, submission.Id);
    }

    private async Task<FormSchema> ReadSchemaAsync(long formId)
    {
        await using var reportingDb = CreateReportingDbContext();
        return await reportingDb.FormSchemas.AsNoTracking().SingleAsync(schema => schema.FormId == formId, Cancellation);
    }

    private AppDbContext CreateAppDbContext()
    {
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigurePostgreSqlOptions(optionsBuilder, fixture.ConnectionString);
        return new AppDbContext(
            optionsBuilder.Options,
            new IntegrationTenantContext(TenantId),
            new OutboxIntegrationEventDispatcher());
    }

    private ReportingDbContext CreateReportingDbContext() =>
        new(
            ReportingTestSchema.ConfigureOptionsBuilder(fixture.ConnectionString).Options,
            new IntegrationTenantContext(TenantId));

    private sealed record SeededForm(long FormId, IReadOnlyList<long> DefinitionIds, long SubmissionId);

    /// <summary>Lets every party go on once all have arrived, or once the timeout passes.</summary>
    private sealed class Rendezvous(int parties)
    {
        private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public async Task ArriveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrived) == parties)
            {
                _allArrived.TrySetResult();
            }

            await Task.WhenAny(_allArrived.Task, Task.Delay(BothReadTimeout, cancellationToken));
        }
    }

    // Holds each rebuild after it has read the schema, so rebuilds that are not kept apart both read it before
    // either saves. A rebuild that has to wait for the lock arrives only after the other one committed.
    private sealed class WaitAfterLockedRead(IFormSchemaRepository inner, Rendezvous rendezvous) : IFormSchemaRepository
    {
        public Task<FormSchema?> GetByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
            inner.GetByFormIdAsync(tenantId, formId, cancellationToken);

        public async Task<FormSchema?> LockAndGetByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken)
        {
            var schema = await inner.LockAndGetByFormIdAsync(tenantId, formId, cancellationToken);
            await rendezvous.ArriveAsync(cancellationToken);
            return schema;
        }

        public Task SaveAsync(FormSchema schema, CancellationToken cancellationToken) =>
            inner.SaveAsync(schema, cancellationToken);

        public Task<int> DeleteByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
            inner.DeleteByFormIdAsync(tenantId, formId, cancellationToken);
    }
}
