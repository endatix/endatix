using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Repositories;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.Outbox;
using Endatix.Modules.Reporting.Persistence;
using Endatix.Persistence.PostgreSql.Locking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.IntegrationTests;

/// <summary>
/// PostgreSQL coverage for FormSchema replace vs merge gate (#892).
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class FormSchemaProcessorReplaceMergeIntegrationTests
{
    private const long TenantId = 51;
    private const long OtherFormFlattenedSubmissionId = 9001;

    private static readonly string DefinitionWithOrphan = """
        {
          "pages": [
            {
              "name": "p1",
              "elements": [
                { "type": "text", "name": "orphan", "title": "Orphan" },
                { "type": "text", "name": "keep", "title": "Keep" }
              ]
            }
          ]
        }
        """;

    private static readonly string DefinitionWithoutOrphan = """
        {
          "pages": [
            {
              "name": "p1",
              "elements": [
                { "type": "text", "name": "keep", "title": "Keep" }
              ]
            }
          ]
        }
        """;

    private readonly DbIntegrationFixture _fixture;

    public FormSchemaProcessorReplaceMergeIntegrationTests(DbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProcessAsync_WithZeroRealSubmissions_ReplacesSchemaAndDeletesFlattenedRows()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedFormAsync(seedRealSubmission: false, seedTestSubmission: false, cancellationToken);
        await SeedReportingStateWithOrphanAsync(seed, cancellationToken);

        FormDefinition currentDefinition = new(TenantId, jsonData: DefinitionWithoutOrphan) { Id = seed.FormDefinitionId };
        var formsRepository = FormsRepositoryReading(currentDefinition);

        await using var reportingDb = CreateReportingDbContext();
        await using var appDb = CreateAppDbContext();
        var processor = CreateProcessor(formsRepository, reportingDb, appDb);

        // Act
        await processor.ProcessAsync(TenantId, seed.FormId, seed.FormDefinitionId, cancellationToken: cancellationToken);

        // Assert
        reportingDb.ChangeTracker.Clear();
        var schema = await reportingDb.FormSchemas
            .SingleOrDefaultAsync(row => row.TenantId == TenantId && row.FormId == seed.FormId, cancellationToken);
        schema.Should().NotBeNull();
        schema!.FlatteningMap.Should().Contain("keep");
        schema.FlatteningMap.Should().NotContain("orphan");
        schema.Codebook.Should().Contain("keep");
        schema.Codebook.Should().NotContain("\"orphan\"");

        var flattenedForForm = await reportingDb.FlattenedSubmissions
            .IgnoreQueryFilters()
            .CountAsync(row => row.TenantId == TenantId && row.FormId == seed.FormId, cancellationToken);
        flattenedForForm.Should().Be(0);

        // Other form's flattened row must remain
        var otherFormRows = await reportingDb.FlattenedSubmissions
            .IgnoreQueryFilters()
            .CountAsync(row => row.SubmissionId == OtherFormFlattenedSubmissionId, cancellationToken);
        otherFormRows.Should().Be(1);
    }

    [Fact]
    public async Task ProcessAsync_WithOnlyTestSubmissions_ReplacesSchemaAndDeletesFlattenedRows()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedFormAsync(seedRealSubmission: false, seedTestSubmission: true, cancellationToken);
        await SeedReportingStateWithOrphanAsync(seed, cancellationToken);

        FormDefinition currentDefinition = new(TenantId, jsonData: DefinitionWithoutOrphan) { Id = seed.FormDefinitionId };
        var formsRepository = FormsRepositoryReading(currentDefinition);

        await using var reportingDb = CreateReportingDbContext();
        await using var appDb = CreateAppDbContext();
        var processor = CreateProcessor(formsRepository, reportingDb, appDb);

        // Act
        await processor.ProcessAsync(TenantId, seed.FormId, seed.FormDefinitionId, cancellationToken: cancellationToken);

        // Assert
        reportingDb.ChangeTracker.Clear();
        var schema = await reportingDb.FormSchemas
            .SingleAsync(row => row.TenantId == TenantId && row.FormId == seed.FormId, cancellationToken);
        schema.FlatteningMap.Should().Contain("keep");
        schema.FlatteningMap.Should().NotContain("orphan");

        var flattenedForForm = await reportingDb.FlattenedSubmissions
            .IgnoreQueryFilters()
            .CountAsync(row => row.TenantId == TenantId && row.FormId == seed.FormId, cancellationToken);
        flattenedForForm.Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_WithRealSubmission_MergesAndKeepsFlattenedRows()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedFormAsync(seedRealSubmission: true, seedTestSubmission: false, cancellationToken);
        await SeedReportingStateWithOrphanAsync(seed, cancellationToken);

        FormDefinition currentDefinition = new(TenantId, jsonData: DefinitionWithoutOrphan) { Id = seed.FormDefinitionId };
        var formsRepository = FormsRepositoryReading(currentDefinition);

        await using var reportingDb = CreateReportingDbContext();
        await using var appDb = CreateAppDbContext();
        var processor = CreateProcessor(formsRepository, reportingDb, appDb);

        // Act
        await processor.ProcessAsync(TenantId, seed.FormId, seed.FormDefinitionId, cancellationToken: cancellationToken);

        // Assert
        reportingDb.ChangeTracker.Clear();
        var schema = await reportingDb.FormSchemas
            .SingleAsync(row => row.TenantId == TenantId && row.FormId == seed.FormId, cancellationToken);
        schema.FlatteningMap.Should().Contain("orphan");
        schema.FlatteningMap.Should().Contain("keep");

        var flattenedForForm = await reportingDb.FlattenedSubmissions
            .IgnoreQueryFilters()
            .CountAsync(row => row.TenantId == TenantId && row.FormId == seed.FormId, cancellationToken);
        flattenedForForm.Should().Be(1);
    }

    [Fact]
    public async Task ProcessAsync_WithReplaceTrueAndRealSubmissions_ReplacesSchemaAndDeletesFlattenedRows()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedFormAsync(seedRealSubmission: true, seedTestSubmission: false, cancellationToken);
        await SeedReportingStateWithOrphanAsync(seed, cancellationToken);

        FormDefinition currentDefinition = new(TenantId, jsonData: DefinitionWithoutOrphan) { Id = seed.FormDefinitionId };
        var formsRepository = FormsRepositoryReading(currentDefinition);

        await using var reportingDb = CreateReportingDbContext();
        await using var appDb = CreateAppDbContext();
        var processor = CreateProcessor(formsRepository, reportingDb, appDb);

        // Act
        await processor.ProcessAsync(
            TenantId,
            seed.FormId,
            seed.FormDefinitionId,
            replace: true,
            cancellationToken);

        // Assert
        reportingDb.ChangeTracker.Clear();
        var schema = await reportingDb.FormSchemas
            .SingleAsync(row => row.TenantId == TenantId && row.FormId == seed.FormId, cancellationToken);
        schema.FlatteningMap.Should().Contain("keep");
        schema.FlatteningMap.Should().NotContain("orphan");

        var flattenedForForm = await reportingDb.FlattenedSubmissions
            .IgnoreQueryFilters()
            .CountAsync(row => row.TenantId == TenantId && row.FormId == seed.FormId, cancellationToken);
        flattenedForForm.Should().Be(0);

        var otherFormRows = await reportingDb.FlattenedSubmissions
            .IgnoreQueryFilters()
            .CountAsync(row => row.SubmissionId == OtherFormFlattenedSubmissionId, cancellationToken);
        otherFormRows.Should().Be(1);
    }

    [Fact]
    public async Task Compile_removes_its_schema_when_the_form_is_deleted_while_it_compiles()
    {
        // Arrange — the form is deleted, and its deletion synced, between the compile's read and its write.
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedFormAsync(seedRealSubmission: false, seedTestSubmission: false, cancellationToken);

        await using var reportingDb = CreateReportingDbContext();
        await using var appDb = CreateAppDbContext();
        FormsRepository forms = new(appDb, new AppUnitOfWork(appDb), new EndatixSpecificationEvaluator([]));
        var formsRepository = Substitute.For<IFormsRepository>();
        formsRepository
            .SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var definition = await forms.SingleOrDefaultAsync(
                    call.Arg<DefinitionByFormAndDefinitionIdSpec>(),
                    cancellationToken);
                await DeleteFormAndSyncAsync(seed.FormId, cancellationToken);
                return definition;
            });
        formsRepository
            .AnyAsync(Arg.Any<FormSpecifications.ById>(), Arg.Any<CancellationToken>())
            .Returns(call => forms.AnyAsync(call.Arg<FormSpecifications.ById>(), cancellationToken));
        var processor = CreateProcessor(formsRepository, reportingDb, appDb);

        // Act
        await processor.ProcessAsync(TenantId, seed.FormId, seed.FormDefinitionId, cancellationToken: cancellationToken);

        // Assert
        reportingDb.ChangeTracker.Clear();
        var schemas = await reportingDb.FormSchemas
            .CountAsync(row => row.TenantId == TenantId && row.FormId == seed.FormId, cancellationToken);
        schemas.Should().Be(0);
    }

    // Deletes the form as the API does, then removes its Reporting rows as its deletion sync does.
    private async Task DeleteFormAndSyncAsync(long formId, CancellationToken cancellationToken)
    {
        await using (var appDb = CreateAppDbContext())
        {
            var form = await appDb.Forms
                .Include(form => form.FormDefinitions)
                .SingleAsync(form => form.Id == formId, cancellationToken);
            form.Delete();
            await appDb.SaveChangesAsync(cancellationToken);
        }

        await using var reportingDb = CreateReportingDbContext();
        ReportingUnitOfWork unitOfWork = new(reportingDb);
        SyncFormDeletionOutboxHandler sync = new(
            new FormSchemaRepository(reportingDb, unitOfWork, new PostgreSqlTransactionLock()),
            new FlattenedSubmissionRepository(reportingDb, unitOfWork),
            unitOfWork,
            NullLogger<SyncFormDeletionOutboxHandler>.Instance);
        await sync.ProcessAsync(new SyncFormDeletionOutboxHandler.Input(TenantId, formId), outboxMessageId: 0, cancellationToken);
    }

    private async Task<SeededForm> SeedFormAsync(
        bool seedRealSubmission,
        bool seedTestSubmission,
        CancellationToken cancellationToken)
    {
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);

        await using var appDb = CreateAppDbContext();
        Tenant tenant = new("form-schema-replace-tenant", "tnntfsrm") { Id = TenantId };
        appDb.Set<Tenant>().Add(tenant);
        await appDb.SaveChangesAsync(cancellationToken);

        var form = Form.Create(new FormCreateArgs(TenantId: TenantId, Name: "Replace/merge form"));
        appDb.Forms.Add(form);
        await appDb.SaveChangesAsync(cancellationToken);

        FormDefinition definition = new(TenantId, isDraft: false, jsonData: DefinitionWithOrphan);
        form.AddFormDefinition(definition);
        appDb.Set<FormDefinition>().Add(definition);
        await appDb.SaveChangesAsync(cancellationToken);

        var formId = form.Id;
        var formDefinitionId = definition.Id;

        if (seedRealSubmission)
        {
            await SeedSubmissionAsync(appDb, formId, formDefinitionId, isTest: false, cancellationToken);
        }

        if (seedTestSubmission)
        {
            await SeedSubmissionAsync(appDb, formId, formDefinitionId, isTest: true, cancellationToken);
        }

        return new SeededForm(formId, formDefinitionId);
    }

    private async Task SeedReportingStateWithOrphanAsync(SeededForm seed, CancellationToken cancellationToken)
    {
        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(DefinitionWithOrphan);

        await using var reportingDb = CreateReportingDbContext();
        FormSchema schema = new(
            TenantId,
            seed.FormId,
            seed.FormDefinitionId,
            compiled.FlatteningMapJson,
            compiled.CodebookJson,
            compiled.LocalesJson);
        reportingDb.FormSchemas.Add(schema);
        await reportingDb.SaveChangesAsync(cancellationToken);

        FlattenedRowSeed rows = new(reportingDb);
        await rows.ProcessedAsync(
            new FlattenedSubmissionKey(TenantId, seed.FormId, SubmissionId: seed.FormId + 1000),
            """{"keep":"x"}""",
            cancellationToken);
        await rows.ProcessedAsync(
            new FlattenedSubmissionKey(TenantId, seed.FormId + 99, OtherFormFlattenedSubmissionId),
            """{"other":true}""",
            cancellationToken);
    }

    private static async Task SeedSubmissionAsync(
        AppDbContext appDb,
        long formId,
        long formDefinitionId,
        bool isTest,
        CancellationToken cancellationToken)
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: formId,
            FormDefinitionId: formDefinitionId,
            JsonData: """{"keep":"x"}""",
            IsComplete: true,
            IsTestSubmission: isTest));
        appDb.Submissions.Add(submission);
        await appDb.SaveChangesAsync(cancellationToken);
        appDb.ChangeTracker.Clear();
    }

    // The form is read as the given definition and still exists after the compile wrote its schema.
    private static IFormsRepository FormsRepositoryReading(FormDefinition definition)
    {
        var formsRepository = Substitute.For<IFormsRepository>();
        formsRepository
            .SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(definition);
        formsRepository
            .AnyAsync(Arg.Any<FormSpecifications.ById>(), Arg.Any<CancellationToken>())
            .Returns(true);
        return formsRepository;
    }

    private static FormSchemaProcessor CreateProcessor(
        IFormsRepository formsRepository,
        ReportingDbContext reportingDb,
        AppDbContext appDb)
    {
        ReportingUnitOfWork unitOfWork = new(reportingDb);
        return new FormSchemaProcessor(
            formsRepository,
            new FormSchemaRepository(reportingDb, unitOfWork, new PostgreSqlTransactionLock()),
            new FlattenedSubmissionRepository(reportingDb, unitOfWork),
            unitOfWork,
            appDb,
            new FormSchemaCompiler(),
            NullLogger<FormSchemaProcessor>.Instance);
    }

    private AppDbContext CreateAppDbContext()
    {
        IntegrationTenantContext tenantContext = new(TenantId);

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigurePostgreSqlOptions(optionsBuilder, _fixture.ConnectionString);

        return new AppDbContext(
            optionsBuilder.Options,
            tenantContext,
            new OutboxIntegrationEventDispatcher());
    }

    private ReportingDbContext CreateReportingDbContext()
    {
        IntegrationTenantContext tenantContext = new(TenantId);

        var optionsBuilder =
            ReportingTestSchema.ConfigureOptionsBuilder(_fixture.ConnectionString);

        return new ReportingDbContext(optionsBuilder.Options, tenantContext);
    }

    private sealed record SeededForm(long FormId, long FormDefinitionId);
}
