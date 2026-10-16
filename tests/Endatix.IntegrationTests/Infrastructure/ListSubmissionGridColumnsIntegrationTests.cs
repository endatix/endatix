using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

/// <summary>
/// <see cref="ListSubmissionGridColumns"/> against a real database: preparation counts, tenant isolation and the
/// columns of a compiled schema.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class ListSubmissionGridColumnsIntegrationTests(DbIntegrationFixture fixture)
{
    private const long TenantId = 51;
    private const long OtherTenantId = 52;

    private const string FlatteningMapJson =
        """{"version":1,"columns":[{"key":"city","kind":"Simple","label":"city","dataType":"string"}]}""";

    private const string CodebookJson =
        """
        {"version":1,"locales":["default"],
         "questions":{"city":{"surveyJsType":"text","title":{"default":"City"},"pageName":"about","pageTitle":"About you"}},
         "columns":{"city":{"parentKey":"city","surveyJsType":"text","title":{"default":"City"}}},
         "choiceCatalogs":{}}
        """;

    [Fact]
    public async Task ExecuteAsync_FormWithoutSchema_CountsPreparationAndReturnsNoColumns()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);
        await using var appDb = CreateAppDbContext(TenantId);
        await using var reportingDb = CreateReportingDbContext(TenantId);

        // Act
        SubmissionGridColumnsQuery query = new(TenantId, seed.FormId, "default");
        var result = await CreateSut(reportingDb, appDb).ExecuteAsync(query, cancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.SchemaReady.Should().BeFalse();
        result.Value.Columns.Should().BeEmpty();
        result.Value.Readiness.Should().Be(new SubmissionGridReadiness(
            Submissions: 4,
            CompletedSubmissions: 3,
            Prepared: 1,
            Pending: 1,
            Failed: 1,
            SkippedInProgress: 0));
    }

    [Fact]
    public async Task ExecuteAsync_FormWithSchema_ReturnsColumnsWithFilterTypeAndPage()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);
        await using var reportingDb = CreateReportingDbContext(TenantId);
        reportingDb.FormSchemas.Add(new FormSchema(TenantId, seed.FormId, 1, FlatteningMapJson, CodebookJson));
        await reportingDb.SaveChangesAsync(cancellationToken);
        await using var appDb = CreateAppDbContext(TenantId);

        // Act
        SubmissionGridColumnsQuery query = new(TenantId, seed.FormId, "default");
        var result = await CreateSut(reportingDb, appDb).ExecuteAsync(query, cancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.SchemaReady.Should().BeTrue();
        result.Value.Columns.Should().ContainSingle().Which.Should().Be(new SubmissionGridColumn(
            Key: "city",
            Kind: "Simple",
            Title: "City",
            QuestionType: "text",
            FilterType: SubmissionGridFilterTypes.Text,
            PageName: "about",
            PageTitle: "About you",
            PanelTitle: null));
    }

    [Fact]
    public async Task ExecuteAsync_FormOfAnotherTenant_ReturnsNotFound()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);
        await using var appDb = CreateAppDbContext(OtherTenantId);
        await using var reportingDb = CreateReportingDbContext(OtherTenantId);

        // Act
        SubmissionGridColumnsQuery query = new(OtherTenantId, seed.FormId, "default");
        var result = await CreateSut(reportingDb, appDb).ExecuteAsync(query, cancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutTenant_ReturnsUnauthorized()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);
        await using var appDb = CreateAppDbContext(TenantId);
        await using var reportingDb = CreateReportingDbContext(TenantId);

        // Act
        SubmissionGridColumnsQuery query = new(0, seed.FormId, "default");
        var result = await CreateSut(reportingDb, appDb).ExecuteAsync(query, cancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Unauthorized);
    }

    // Four submissions: completed and prepared, completed and pending, completed and failed, in progress and never
    // queued. A deleted completed submission is not counted. The other tenant gets a tenant row only.
    private async Task<SeededForm> SeedAsync(CancellationToken cancellationToken)
    {
        await fixture.Checkpoint.ResetAsync(fixture.ConnectionString, fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(fixture.ConnectionString, fixture.Provider, cancellationToken);

        await using var appDb = CreateAppDbContext(TenantId);
        appDb.Set<Tenant>().Add(new Tenant("grid-columns-tenant", "tnntgrdc") { Id = TenantId });
        appDb.Set<Tenant>().Add(new Tenant("grid-columns-other", "tnntgrdo") { Id = OtherTenantId });
        await appDb.SaveChangesAsync(cancellationToken);

        var form = Form.Create(new FormCreateArgs(TenantId: TenantId, Name: "Grid columns form"));
        appDb.Forms.Add(form);
        await appDb.SaveChangesAsync(cancellationToken);

        FormDefinition definition = new(TenantId, isDraft: false, jsonData: """{"pages":[]}""");
        form.AddFormDefinition(definition);
        appDb.Set<FormDefinition>().Add(definition);
        await appDb.SaveChangesAsync(cancellationToken);

        SubmissionSeed submissions = new(appDb, form.Id, definition.Id);
        var prepared = await submissions.AddAsync(isComplete: true, cancellationToken);
        var pending = await submissions.AddAsync(isComplete: true, cancellationToken);
        var failed = await submissions.AddAsync(isComplete: true, cancellationToken);
        await submissions.AddAsync(isComplete: false, cancellationToken);
        var deleted = await submissions.AddAsync(isComplete: true, cancellationToken);
        await appDb.Submissions
            .Where(submission => submission.Id == deleted)
            .ExecuteUpdateAsync(updates => updates.SetProperty(submission => submission.IsDeleted, true), cancellationToken);

        await using var reportingDb = CreateReportingDbContext(TenantId);
        FlattenedRowSeed rows = new(reportingDb);
        await rows.ProcessedAsync(new FlattenedSubmissionKey(TenantId, form.Id, prepared), "{}", cancellationToken);
        await rows.PendingAsync(new FlattenedSubmissionKey(TenantId, form.Id, pending), cancellationToken);
        await rows.PendingAsync(new FlattenedSubmissionKey(TenantId, form.Id, failed), cancellationToken);
        await rows.FailedAsync(new FlattenedSubmissionKey(TenantId, form.Id, failed), "seeded failure", cancellationToken);

        return new SeededForm(form.Id);
    }

    private static ListSubmissionGridColumns CreateSut(ReportingDbContextBase reportingDb, AppDbContext appDb) =>
        new(
            new FormSchemaRepository(
                reportingDb,
                new ReportingUnitOfWork(reportingDb),
                ReportingTestSchema.TransactionLockFor(reportingDb)),
            reportingDb,
            appDb);

    private AppDbContext CreateAppDbContext(long tenantId)
    {
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigureOptions(optionsBuilder, fixture.ConnectionString, fixture.Provider);

        return new AppDbContext(
            optionsBuilder.Options,
            new IntegrationTenantContext(tenantId),
            new OutboxIntegrationEventDispatcher());
    }

    private ReportingDbContextBase CreateReportingDbContext(long tenantId) =>
        ReportingTestSchema.CreateContext(fixture.ConnectionString, fixture.Provider, new IntegrationTenantContext(tenantId));

    private sealed record SeededForm(long FormId);

    private sealed class SubmissionSeed(AppDbContext appDb, long formId, long formDefinitionId)
    {
        public async Task<long> AddAsync(bool isComplete, CancellationToken cancellationToken)
        {
            var submission = Submission.Create(new SubmissionCreateArgs(
                TenantId: TenantId,
                FormId: formId,
                FormDefinitionId: formDefinitionId,
                JsonData: """{"city":"Columbus"}""",
                IsComplete: isComplete));
            appDb.Submissions.Add(submission);
            await appDb.SaveChangesAsync(cancellationToken);

            // OwnsOne Status uses shared static instances; clear tracking before the next Add.
            appDb.ChangeTracker.Clear();
            return submission.Id;
        }
    }
}
