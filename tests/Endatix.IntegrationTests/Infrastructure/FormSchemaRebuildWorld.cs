using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Locking;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Repositories;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.BackgroundJobs;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using Endatix.Modules.Reporting.Features.FormSchema.SubmitterProfile;
using Endatix.Modules.Reporting.Features.Outbox;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.IntegrationTests;

/// <summary>
/// One form with its definitions and submissions in the database, and the Reporting pieces that rebuild its schema,
/// each built in a scope of its own, as a job or a request would be. The pieces copy the given submitter profile
/// fields, none by default.
/// </summary>
internal sealed class FormSchemaRebuildWorld(DbIntegrationFixture fixture, SubmitterProfileFields? submitterProfileFields = null)
{
    public const long TenantId = 52;

    public const string BaseDefinition = """
        { "pages": [ { "elements": [ { "type": "text", "name": "q0", "title": "Base" } ] } ] }
        """;

    public const string AddsQuestionA = """
        { "pages": [ { "elements": [
          { "type": "text", "name": "q0", "title": "Base" },
          { "type": "text", "name": "qa", "title": "Added by A" }
        ] } ] }
        """;

    public const string AddsQuestionAInGerman = """
        { "pages": [ { "elements": [
          { "type": "text", "name": "q0", "title": { "default": "Base", "de": "Basis" } },
          { "type": "text", "name": "qa", "title": "Added by A" }
        ] } ] }
        """;

    public const string AddsQuestionB = """
        { "pages": [ { "elements": [
          { "type": "text", "name": "q0", "title": "Base" },
          { "type": "text", "name": "qb", "title": "Added by B" }
        ] } ] }
        """;

    public const string RetitlesBase = """
        { "pages": [ { "elements": [
          { "type": "text", "name": "q0", "title": "Base, retitled" },
          { "type": "text", "name": "qb", "title": "Added by B" }
        ] } ] }
        """;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public static IEnumerable<string> ColumnKeys(FormSchema schema) =>
        FormSchemaFlatteningMap.FromJson(schema.FlatteningMap).Columns.Select(column => column.Key);

    public static TransactionLockRequest RebuildLockOf(long formId) =>
        new(TransactionLockScopes.ReportingFormSchema, $"{TenantId}:{formId}");

    public RebuildScope OpenScope() =>
        new(CreateAppDbContext(), CreateReportingDbContext(), submitterProfileFields ?? SubmitterProfileFields.None);

    // One real submission on the oldest definition, so every compile merges.
    public async Task<RebuildForm> SeedFormWithRealSubmissionAsync(string[] definitionsOldestFirst)
    {
        var form = await SeedFormAsync(definitionsOldestFirst);
        var submissionId = await AddSubmissionAsync(Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: form.FormId,
            FormDefinitionId: form.DefinitionIds[0],
            JsonData: """{"q0":"first answer"}""",
            IsComplete: true)));
        return form with { SubmissionId = submissionId };
    }

    public async Task<RebuildForm> SeedFormAsync(string[] definitionsOldestFirst)
    {
        await fixture.Checkpoint.ResetAsync(fixture.ConnectionString, fixture.Provider, Cancellation);
        await ReportingTestSchema.EnsureMigratedAsync(fixture.ConnectionString, fixture.Provider, Cancellation);

        await using (var tenantDb = CreateAppDbContext())
        {
            tenantDb.Set<Tenant>().Add(new Tenant("form-schema-rebuild-tenant", "tnntfsrb") { Id = TenantId });
            await tenantDb.SaveChangesAsync(Cancellation);
        }

        return await AddFormAsync(definitionsOldestFirst);
    }

    /// <summary>Another form of the world's tenant, beside the ones already seeded.</summary>
    public async Task<RebuildForm> AddFormAsync(string[] definitionsOldestFirst)
    {
        await using var appDb = CreateAppDbContext();

        // Saved before its definitions, which avoids the Form <-> ActiveDefinition circular insert.
        var form = Form.Create(new FormCreateArgs(TenantId: TenantId, Name: "Rebuilt form"));
        appDb.Forms.Add(form);
        await appDb.SaveChangesAsync(Cancellation);
        return new RebuildForm(form.Id, await AddDefinitionsAsync(appDb, form, definitionsOldestFirst), SubmissionId: 0);
    }

    /// <summary>A completed real submission, so every compile of its form merges.</summary>
    public Task<long> AddRealSubmissionAsync(long formId, long formDefinitionId, string jsonData) =>
        AddSubmissionAsync(Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: formId,
            FormDefinitionId: formDefinitionId,
            JsonData: jsonData,
            IsComplete: true)));

    public Task<long> AddTestSubmissionAsync(long formId, long formDefinitionId, string jsonData) =>
        AddSubmissionAsync(Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: formId,
            FormDefinitionId: formDefinitionId,
            JsonData: jsonData,
            IsComplete: true,
            IsTestSubmission: true)));

    public async Task EditDefinitionAsync(long formDefinitionId, string jsonData)
    {
        await using var appDb = CreateAppDbContext();
        var definition = await appDb.Set<FormDefinition>().SingleAsync(row => row.Id == formDefinitionId, Cancellation);
        definition.UpdateSchema(jsonData);
        await appDb.SaveChangesAsync(Cancellation);
    }

    public async Task CompileAsync(
        long formId,
        long formDefinitionId,
        Func<IFormSchemaRepository, IFormSchemaRepository>? wrapSchemas = null)
    {
        await using var scope = OpenScope();
        var schemas = wrapSchemas is null ? scope.Schemas() : wrapSchemas(scope.Schemas());
        await scope.Processor(schemas).ProcessAsync(TenantId, formId, formDefinitionId, cancellationToken: Cancellation);
    }

    public async Task ReplaceAsync(long formId, long formDefinitionId)
    {
        await using var scope = OpenScope();
        await scope.Processor(scope.Schemas()).ProcessAsync(TenantId, formId, formDefinitionId, replace: true, Cancellation);
    }

    public async Task FlattenAsync(
        long formId,
        long submissionId,
        Func<IFormSchemaRepository, IFormSchemaRepository>? wrapPersonalizationSchemas = null)
    {
        await using var scope = OpenScope();
        await scope.Flattening(scope.TransactionLock, wrapPersonalizationSchemas)
            .ProcessAsync(TenantId, formId, submissionId, Cancellation);
    }

    /// <summary>A completed submission with audience values frozen on it, as an on-behalf create leaves it.</summary>
    public Task<long> AddPersonalizedSubmissionAsync(
        long formId,
        long formDefinitionId,
        string snapshot,
        bool isTestSubmission = false)
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: formId,
            FormDefinitionId: formDefinitionId,
            JsonData: """{"q0":"answer"}""",
            IsComplete: true,
            IsTestSubmission: isTestSubmission));
        submission.FreezeOnBehalf(snapshot);
        return AddSubmissionAsync(submission);
    }

    /// <summary>
    /// Gives a submission a new submitter profile snapshot, and returns the outbox message the change wrote.
    /// </summary>
    public async Task<OutboxMessage> ChangeSubmitterAsync(long submissionId, string profileSnapshot)
    {
        await using var appDb = CreateAppDbContext();
        var submission = await appDb.Submissions.SingleAsync(row => row.Id == submissionId, Cancellation);
        submission.SetSubmitter(submission.SubmitterId, submission.SubmitterDisplayId, profileSnapshot);
        await appDb.SaveChangesAsync(Cancellation);
        return await appDb.OutboxMessages.AsNoTracking()
            .Where(message => message.EventType == SubmissionUpdatedEvent.EventTypeName)
            .OrderByDescending(message => message.Id)
            .FirstAsync(Cancellation);
    }

    // Runs the flatten as the background job runs it, from the outbox message of the submission's completion.
    public Task<Result> RunFlattenJobAsync(long formId, long submissionId, ITransactionLock transactionLock)
    {
        OutboxMessage message = new(
            SubmissionCompletedEvent.EventTypeName,
            $$"""{"tenantId":"{{TenantId}}","formId":"{{formId}}","submissionId":"{{submissionId}}"}""",
            TenantId,
            DateTime.UtcNow,
            1) { Id = 700 };
        return RunFlattenJobAsync(message, transactionLock);
    }

    // Runs the flatten as the background job runs it, from the given outbox message.
    public async Task<Result> RunFlattenJobAsync(OutboxMessage message, ITransactionLock transactionLock)
    {
        BackgroundJobContext job = new(
            JobId: 1,
            FlattenSubmissionOutboxHandler.JobType,
            TenantId,
            BackgroundJobPayloadSerializer.Serialize(new OutboxMessageJob<FlattenSubmissionOutboxHandler>(message.Id)),
            AttemptCount: 1);
        await using var scope = OpenScope();
        return await scope.FlattenJob(transactionLock, message).ExecuteAsync(job, Cancellation);
    }

    public async Task<SubmissionBackfillResult> BackfillAsync(long formId, ITransactionLock transactionLock)
    {
        await using var scope = OpenScope();
        return await scope.Backfill(transactionLock)
            .BackfillFormAsync(TenantId, formId, new SubmissionBackfillOptions(), Cancellation);
    }

    public async Task<FormSchema> ReadSchemaAsync(long formId)
    {
        await using var reportingDb = CreateReportingDbContext();
        return await reportingDb.FormSchemas.AsNoTracking().SingleAsync(schema => schema.FormId == formId, Cancellation);
    }

    public async Task<FlattenedSubmission> ReadFlattenedAsync(long submissionId)
    {
        await using var reportingDb = CreateReportingDbContext();
        return await reportingDb.FlattenedSubmissions.AsNoTracking()
            .SingleAsync(row => row.SubmissionId == submissionId, Cancellation);
    }

    public ReportingDbContextBase CreateReportingDbContext() =>
        ReportingTestSchema.CreateContext(
            fixture.ConnectionString,
            fixture.Provider,
            new IntegrationTenantContext(TenantId));

    private static async Task<List<long>> AddDefinitionsAsync(AppDbContext appDb, Form form, string[] definitionsOldestFirst)
    {
        List<long> definitionIds = [];
        foreach (var json in definitionsOldestFirst)
        {
            FormDefinition definition = new(TenantId, isDraft: false, jsonData: json);
            form.AddFormDefinition(definition);
            appDb.Set<FormDefinition>().Add(definition);
            await appDb.SaveChangesAsync(Cancellation);
            definitionIds.Add(definition.Id);
        }

        return definitionIds;
    }

    private async Task<long> AddSubmissionAsync(Submission submission)
    {
        await using var appDb = CreateAppDbContext();
        appDb.Submissions.Add(submission);
        await appDb.SaveChangesAsync(Cancellation);
        return submission.Id;
    }

    internal AppDbContext CreateAppDbContext()
    {
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigureOptions(optionsBuilder, fixture.ConnectionString, fixture.Provider);
        return new AppDbContext(
            optionsBuilder.Options,
            new IntegrationTenantContext(TenantId),
            new OutboxIntegrationEventDispatcher());
    }
}

internal sealed record RebuildForm(long FormId, IReadOnlyList<long> DefinitionIds, long SubmissionId);

/// <summary>The two databases of one job or request, and the Reporting pieces built on them.</summary>
internal sealed class RebuildScope(
    AppDbContext appDb,
    ReportingDbContextBase reportingDb,
    SubmitterProfileFields submitterProfileFields) : IAsyncDisposable
{
    /// <summary>The transaction lock the host registers for the database under test.</summary>
    public ITransactionLock TransactionLock { get; } = ReportingTestSchema.TransactionLockFor(reportingDb);

    public FormSchemaRepository Schemas(ITransactionLock? transactionLock = null) =>
        new(reportingDb, new ReportingUnitOfWork(reportingDb), transactionLock ?? TransactionLock);

    public FormSchemaProcessor Processor(IFormSchemaRepository schemas, IFlattenedSubmissionRepository? flattenedRows = null) =>
        Processor(schemas, flattenedRows, Compiler());

    public FormSchemaProcessor Processor(IFormSchemaRepository schemas, RebuildCounter counter) =>
        Processor(schemas, counter.FlattenedRows, counter.Compiler);

    public FormSchemaProvider Provider(IFormSchemaRepository schemas, RebuildCounter? counter = null) =>
        new(
            schemas,
            counter is null ? Processor(schemas) : Processor(schemas, counter),
            new FormSchemaCoverage(FormsRepository(), Compiler()));

    public SubmissionFlatteningProcessor Flattening(
        ITransactionLock transactionLock,
        Func<IFormSchemaRepository, IFormSchemaRepository>? wrapPersonalizationSchemas = null)
    {
        var schemas = Schemas(transactionLock);
        return new(
            Submissions(),
            FlattenedRows(),
            Provider(schemas),
            Personalization(wrapPersonalizationSchemas is null ? schemas : wrapPersonalizationSchemas(schemas)),
            submitterProfileFields,
            NullLogger<SubmissionFlatteningProcessor>.Instance);
    }

    public FormSchemaPersonalization Personalization(IFormSchemaRepository schemas) =>
        new(
            FormsRepository(),
            schemas,
            new ReportingUnitOfWork(reportingDb),
            Compiler(),
            NullLogger<FormSchemaPersonalization>.Instance);

    public IBackgroundJobHandler FlattenJob(ITransactionLock transactionLock, OutboxMessage message)
    {
        var outboxMessages = Substitute.For<IRepository<OutboxMessage>>();
        outboxMessages.FirstOrDefaultAsync(Arg.Any<OutboxMessageByIdForTenantSpec>(), Arg.Any<CancellationToken>())
            .Returns(message);
        return new OutboxJobHandler<FlattenSubmissionOutboxHandler>(
            new OutboxJobMessages(outboxMessages),
            new FlattenSubmissionOutboxHandler(Flattening(transactionLock), NullLogger<FlattenSubmissionOutboxHandler>.Instance),
            new OutboxWorkRegistration<FlattenSubmissionOutboxHandler>(readTenant: null, onlyWhen: null),
            NullLogger<OutboxJobHandler<FlattenSubmissionOutboxHandler>>.Instance);
    }

    public SubmissionBackfillProcessor Backfill(ITransactionLock transactionLock) =>
        new(Submissions(), FlattenedRows(), Flattening(transactionLock), NullLogger<SubmissionBackfillProcessor>.Instance);

    public async ValueTask DisposeAsync()
    {
        await appDb.DisposeAsync();
        await reportingDb.DisposeAsync();
    }

    public FormSchemaProcessor Processor(
        IFormSchemaRepository schemas,
        IFlattenedSubmissionRepository? flattenedRows,
        FormSchemaCompiler compiler)
    {
        ReportingUnitOfWork unitOfWork = new(reportingDb);
        return new FormSchemaProcessor(
            FormsRepository(),
            schemas,
            flattenedRows ?? new FlattenedSubmissionRepository(reportingDb, unitOfWork, new DateTimeProvider()),
            unitOfWork,
            appDb,
            compiler,
            ReportingTestSchema.SearchCopyFor(reportingDb),
            NullLogger<FormSchemaProcessor>.Instance);
    }

    private FormSchemaCompiler Compiler() => new(submitterProfileFields: submitterProfileFields);

    private FlattenedSubmissionRepository FlattenedRows() =>
        new(reportingDb, new ReportingUnitOfWork(reportingDb), new DateTimeProvider());

    private EfRepository<Submission> Submissions() => new(appDb, new EndatixSpecificationEvaluator([]));

    private FormsRepository FormsRepository() => new(appDb, new AppUnitOfWork(appDb), new EndatixSpecificationEvaluator([]));
}
