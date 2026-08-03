using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededForm seed = await SeedFormAsync(seedRealSubmission: false, seedTestSubmission: false, cancellationToken);
        await SeedReportingStateWithOrphanAsync(seed, cancellationToken);

        FormDefinition currentDefinition = new(TenantId, jsonData: DefinitionWithoutOrphan) { Id = seed.FormDefinitionId };
        IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
        formsRepository
            .SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), cancellationToken)
            .Returns(currentDefinition);

        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        await using AppDbContext appDb = CreateAppDbContext();
        FormSchemaProcessor processor = CreateProcessor(formsRepository, reportingDb, appDb);

        // Act
        await processor.ProcessAsync(TenantId, seed.FormId, seed.FormDefinitionId, cancellationToken: cancellationToken);

        // Assert
        reportingDb.ChangeTracker.Clear();
        FormSchema? schema = await reportingDb.FormSchemas
            .SingleOrDefaultAsync(row => row.TenantId == TenantId && row.FormId == seed.FormId, cancellationToken);
        schema.Should().NotBeNull();
        schema!.FlatteningMap.Should().Contain("keep");
        schema.FlatteningMap.Should().NotContain("orphan");
        schema.Codebook.Should().Contain("keep");
        schema.Codebook.Should().NotContain("\"orphan\"");

        int flattenedForForm = await reportingDb.FlattenedSubmissions
            .IgnoreQueryFilters()
            .CountAsync(row => row.TenantId == TenantId && row.FormId == seed.FormId, cancellationToken);
        flattenedForForm.Should().Be(0);

        // Other form's flattened row must remain
        int otherFormRows = await reportingDb.FlattenedSubmissions
            .IgnoreQueryFilters()
            .CountAsync(row => row.SubmissionId == OtherFormFlattenedSubmissionId, cancellationToken);
        otherFormRows.Should().Be(1);
    }

    [Fact]
    public async Task ProcessAsync_WithOnlyTestSubmissions_ReplacesSchemaAndDeletesFlattenedRows()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededForm seed = await SeedFormAsync(seedRealSubmission: false, seedTestSubmission: true, cancellationToken);
        await SeedReportingStateWithOrphanAsync(seed, cancellationToken);

        FormDefinition currentDefinition = new(TenantId, jsonData: DefinitionWithoutOrphan) { Id = seed.FormDefinitionId };
        IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
        formsRepository
            .SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), cancellationToken)
            .Returns(currentDefinition);

        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        await using AppDbContext appDb = CreateAppDbContext();
        FormSchemaProcessor processor = CreateProcessor(formsRepository, reportingDb, appDb);

        // Act
        await processor.ProcessAsync(TenantId, seed.FormId, seed.FormDefinitionId, cancellationToken: cancellationToken);

        // Assert
        reportingDb.ChangeTracker.Clear();
        FormSchema schema = await reportingDb.FormSchemas
            .SingleAsync(row => row.TenantId == TenantId && row.FormId == seed.FormId, cancellationToken);
        schema.FlatteningMap.Should().Contain("keep");
        schema.FlatteningMap.Should().NotContain("orphan");

        int flattenedForForm = await reportingDb.FlattenedSubmissions
            .IgnoreQueryFilters()
            .CountAsync(row => row.TenantId == TenantId && row.FormId == seed.FormId, cancellationToken);
        flattenedForForm.Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_WithRealSubmission_MergesAndKeepsFlattenedRows()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededForm seed = await SeedFormAsync(seedRealSubmission: true, seedTestSubmission: false, cancellationToken);
        await SeedReportingStateWithOrphanAsync(seed, cancellationToken);

        FormDefinition currentDefinition = new(TenantId, jsonData: DefinitionWithoutOrphan) { Id = seed.FormDefinitionId };
        IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
        formsRepository
            .SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), cancellationToken)
            .Returns(currentDefinition);

        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        await using AppDbContext appDb = CreateAppDbContext();
        FormSchemaProcessor processor = CreateProcessor(formsRepository, reportingDb, appDb);

        // Act
        await processor.ProcessAsync(TenantId, seed.FormId, seed.FormDefinitionId, cancellationToken: cancellationToken);

        // Assert
        reportingDb.ChangeTracker.Clear();
        FormSchema schema = await reportingDb.FormSchemas
            .SingleAsync(row => row.TenantId == TenantId && row.FormId == seed.FormId, cancellationToken);
        schema.FlatteningMap.Should().Contain("orphan");
        schema.FlatteningMap.Should().Contain("keep");

        int flattenedForForm = await reportingDb.FlattenedSubmissions
            .IgnoreQueryFilters()
            .CountAsync(row => row.TenantId == TenantId && row.FormId == seed.FormId, cancellationToken);
        flattenedForForm.Should().Be(1);
    }

    [Fact]
    public async Task ProcessAsync_WithReplaceTrueAndRealSubmissions_ReplacesSchemaAndDeletesFlattenedRows()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeededForm seed = await SeedFormAsync(seedRealSubmission: true, seedTestSubmission: false, cancellationToken);
        await SeedReportingStateWithOrphanAsync(seed, cancellationToken);

        FormDefinition currentDefinition = new(TenantId, jsonData: DefinitionWithoutOrphan) { Id = seed.FormDefinitionId };
        IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
        formsRepository
            .SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), cancellationToken)
            .Returns(currentDefinition);

        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        await using AppDbContext appDb = CreateAppDbContext();
        FormSchemaProcessor processor = CreateProcessor(formsRepository, reportingDb, appDb);

        // Act
        await processor.ProcessAsync(
            TenantId,
            seed.FormId,
            seed.FormDefinitionId,
            replace: true,
            cancellationToken);

        // Assert
        reportingDb.ChangeTracker.Clear();
        FormSchema schema = await reportingDb.FormSchemas
            .SingleAsync(row => row.TenantId == TenantId && row.FormId == seed.FormId, cancellationToken);
        schema.FlatteningMap.Should().Contain("keep");
        schema.FlatteningMap.Should().NotContain("orphan");

        int flattenedForForm = await reportingDb.FlattenedSubmissions
            .IgnoreQueryFilters()
            .CountAsync(row => row.TenantId == TenantId && row.FormId == seed.FormId, cancellationToken);
        flattenedForForm.Should().Be(0);

        int otherFormRows = await reportingDb.FlattenedSubmissions
            .IgnoreQueryFilters()
            .CountAsync(row => row.SubmissionId == OtherFormFlattenedSubmissionId, cancellationToken);
        otherFormRows.Should().Be(1);
    }

    private async Task<SeededForm> SeedFormAsync(
        bool seedRealSubmission,
        bool seedTestSubmission,
        CancellationToken cancellationToken)
    {
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);

        await using AppDbContext appDb = CreateAppDbContext();
        Tenant tenant = new("form-schema-replace-tenant") { Id = TenantId };
        appDb.Set<Tenant>().Add(tenant);
        await appDb.SaveChangesAsync(cancellationToken);

        Form form = Form.Create(new FormCreateArgs(TenantId: TenantId, Name: "Replace/merge form"));
        appDb.Forms.Add(form);
        await appDb.SaveChangesAsync(cancellationToken);

        FormDefinition definition = new(TenantId, isDraft: false, jsonData: DefinitionWithOrphan);
        form.AddFormDefinition(definition);
        appDb.Set<FormDefinition>().Add(definition);
        await appDb.SaveChangesAsync(cancellationToken);

        long formId = form.Id;
        long formDefinitionId = definition.Id;

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
        FormSchemaCompileResult compiled = compiler.CompilePersisted(DefinitionWithOrphan);

        await using ReportingDbContext reportingDb = CreateReportingDbContext();
        FormSchema schema = new(
            TenantId,
            seed.FormId,
            seed.FormDefinitionId,
            compiled.FlatteningMapJson,
            compiled.CodebookJson,
            compiled.LocalesJson);
        reportingDb.FormSchemas.Add(schema);

        FlattenedSubmission formRow = new(submissionId: seed.FormId + 1000, TenantId, seed.FormId);
        formRow.MarkProcessed("""{"keep":"x"}""");
        reportingDb.FlattenedSubmissions.Add(formRow);

        FlattenedSubmission otherFormRow = new(OtherFormFlattenedSubmissionId, TenantId, formId: seed.FormId + 99);
        otherFormRow.MarkProcessed("""{"other":true}""");
        reportingDb.FlattenedSubmissions.Add(otherFormRow);

        await reportingDb.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSubmissionAsync(
        AppDbContext appDb,
        long formId,
        long formDefinitionId,
        bool isTest,
        CancellationToken cancellationToken)
    {
        Submission submission = Submission.Create(new SubmissionCreateArgs(
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

    private static FormSchemaProcessor CreateProcessor(
        IFormsRepository formsRepository,
        ReportingDbContext reportingDb,
        AppDbContext appDb)
    {
        ReportingUnitOfWork unitOfWork = new(reportingDb);
        return new FormSchemaProcessor(
            formsRepository,
            new FormSchemaRepository(reportingDb, unitOfWork),
            new FlattenedSubmissionRepository(reportingDb, unitOfWork),
            unitOfWork,
            appDb,
            new FormSchemaCompiler(),
            NullLogger<FormSchemaProcessor>.Instance);
    }

    private AppDbContext CreateAppDbContext()
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId);

        IncrementingIdGenerator idGenerator = new();
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigurePostgreSqlOptions(optionsBuilder, _fixture.ConnectionString);

        return new AppDbContext(
            optionsBuilder.Options,
            idGenerator,
            tenantContext,
            new EfCoreValueGeneratorFactory(idGenerator),
            new OutboxIntegrationEventDispatcher());
    }

    private ReportingDbContext CreateReportingDbContext()
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId);

        DbContextOptionsBuilder<ReportingDbContext> optionsBuilder =
            ReportingTestSchema.ConfigureOptionsBuilder(_fixture.ConnectionString);

        return new ReportingDbContext(optionsBuilder.Options, new IncrementingIdGenerator(), tenantContext);
    }

    private sealed record SeededForm(long FormId, long FormDefinitionId);
}
