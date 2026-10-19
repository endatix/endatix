using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions.ListByFormId;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

/// <summary>
/// <see cref="SubmissionGridAnswerSearch"/> against a real database: answer filters on the search copy, joined to
/// the core submissions in one query, with the list's metadata filters and the count of rows it could not check.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class SubmissionGridAnswerSearchIntegrationTests(DbIntegrationFixture fixture)
{

    private const long TenantId = 73;

    private static readonly string SearchKeysJson = $$"""
        [
          {"key":"city","source":"auto","filterType":"text","addedAt":"2026-10-16T00:00:00Z"},
          {"key":"company","source":"auto","filterType":"text","addedAt":"2026-10-16T00:00:00Z"},
          {"key":"visit","source":"auto","filterType":"date","addedAt":"2026-10-16T00:00:00Z"},
          {"key":"arrival","source":"auto","filterType":"datetime","addedAt":"2026-10-16T00:00:00Z"}
        ]
        """;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private TestDatabaseProvider Provider => fixture.Provider;

    [Fact]
    public async Task Contains_ignores_case_and_counts_the_rows_it_could_not_check()
    {
        // Arrange
        var world = await SeedAsync(withSchema: true);
        var columbus = await world.PreparedAsync("""{"city":"columbus"}""");
        await world.PreparedAsync("""{"city":"dayton"}""");
        await world.StaleAsync("""{"city":"columbus"}""");
        await world.InProgressAsync();

        // Act
        var page = await world.SearchAsync([Answer("contains", "city", "COLUM")]);

        // Assert
        page.Items.Select(row => row.Id).Should().Equal(columbus);
        page.TotalRecords.Should().Be(1);
        page.AnswerFilterCoverage.Should().Be(new AnswerFilterCoverage(UncheckedCompleted: 1, UncheckedInProgress: 1));
    }

    [Fact]
    public async Task From_and_to_keep_whole_calendar_days_for_dates_and_date_times()
    {
        // Arrange
        var world = await SeedAsync(withSchema: true);
        await world.PreparedAsync("""{"visit":"2026-02-27","arrival":"2026-02-27T23:59:59"}""");
        var onTheDay = await world.PreparedAsync("""{"visit":"2026-02-28","arrival":"2026-02-28T23:59:59"}""");
        await world.PreparedAsync("""{"visit":"2026-03-01","arrival":"2026-03-01T00:00:00"}""");

        // Act
        var byDate = await world.SearchAsync([Answer("from", "visit", "2026-02-28"), Answer("to", "visit", "2026-02-28")]);
        var byDateTime = await world.SearchAsync([Answer("from", "arrival", "2026-02-28"), Answer("to", "arrival", "2026-02-28")]);

        // Assert
        byDate.Items.Select(row => row.Id).Should().Equal(onTheDay);
        byDateTime.Items.Select(row => row.Id).Should().Equal(onTheDay);
    }

    [Fact]
    public async Task To_on_the_last_calendar_day_keeps_every_dated_answer()
    {
        // Arrange
        var world = await SeedAsync(withSchema: true);
        var lastDay = await world.PreparedAsync("""{"visit":"9999-12-31"}""");
        var earlier = await world.PreparedAsync("""{"visit":"2026-02-28"}""");

        // Act
        var page = await world.SearchAsync([Answer("to", "visit", "9999-12-31")]);

        // Assert
        page.Items.Select(row => row.Id).Should().BeEquivalentTo([lastDay, earlier]);
    }

    [Fact]
    public async Task Filters_on_two_columns_combine_with_and()
    {
        // Arrange
        var world = await SeedAsync(withSchema: true);
        var both = await world.PreparedAsync("""{"city":"columbus","company":"acme"}""");
        await world.PreparedAsync("""{"city":"columbus","company":"globex"}""");
        await world.PreparedAsync("""{"city":"dayton","company":"acme"}""");

        // Act
        var page = await world.SearchAsync([Answer("contains", "city", "columbus"), Answer("contains", "company", "acme")]);

        // Assert
        page.Items.Select(row => row.Id).Should().Equal(both);
    }

    [Fact]
    public async Task Metadata_filters_sort_and_paging_apply_to_the_matches()
    {
        // Arrange
        var world = await SeedAsync(withSchema: true);
        var first = await world.PreparedAsync("""{"city":"columbus"}""", profile: """{"region":"west"}""");
        await world.PreparedAsync("""{"city":"columbus"}""", profile: """{"region":"east"}""");
        var third = await world.PreparedAsync("""{"city":"columbus"}""", profile: """{"region":"west"}""");
        ListByFormIdQuery list = new(
            world.FormId,
            Page: 1,
            PageSize: 1,
            FilterExpressions: ["submitterProfile.region:west", "isComplete:true"],
            SortBy: SubmissionListSortBy.Id,
            SortDescending: true);

        // Act
        var page = await world.SearchAsync([Answer("contains", "city", "col")], list);

        // Assert
        page.Items.Select(row => row.Id).Should().Equal(third);
        page.TotalRecords.Should().Be(2);
        page.TotalPages.Should().Be(2);
        page.AnswerFilterCoverage.Should().Be(new AnswerFilterCoverage(0, 0));
        first.Should().BeLessThan(third);
    }

    [Fact]
    public async Task Rows_return_prepared_answers_for_the_requested_columns()
    {
        // Arrange
        var world = await SeedAsync(withSchema: true);
        await world.PreparedAsync("""{"city":"columbus"}""", dataJson: """{"city":"Columbus","notes":"hello"}""");

        // Act
        var page = await world.SearchAsync([Answer("contains", "city", "col")], columns: ["notes"]);

        // Assert
        var row = page.Items.Should().ContainSingle().Subject;
        row.RowState.Should().Be(SubmissionGridRowStates.Prepared);
        row.Answers["notes"].GetString().Should().Be("hello");
    }

    [Fact]
    public async Task A_key_that_is_not_a_search_key_or_an_operator_its_column_lacks_is_invalid()
    {
        // Arrange
        var world = await SeedAsync(withSchema: true);

        // Act
        var unknown = await world.ExecuteAsync([Answer("contains", "notes", "x")]);
        var wrongOperator = await world.ExecuteAsync([Answer("from", "city", "2026-01-01")]);

        // Assert
        unknown.Status.Should().Be(ResultStatus.Invalid);
        unknown.ValidationErrors.Should().ContainSingle(error => error.ErrorCode == AnswerFilterRules.UnknownKeyCode);
        wrongOperator.Status.Should().Be(ResultStatus.Invalid);
        wrongOperator.ValidationErrors.Should().ContainSingle(error => error.ErrorCode == AnswerFilterRules.UnsupportedOperatorCode);
    }

    [Fact]
    public async Task A_form_without_a_prepared_schema_is_a_conflict()
    {
        // Arrange
        var world = await SeedAsync(withSchema: false);

        // Act
        var result = await world.ExecuteAsync([Answer("contains", "city", "x")]);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task A_form_that_does_not_exist_is_not_found()
    {
        // Arrange
        var world = await SeedAsync(withSchema: true);
        ListByFormIdQuery unknownForm = new(world.FormId + 1, 1, 10);

        // Act
        var result = await world.ExecuteAsync([Answer("contains", "city", "x")], unknownForm);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    private static AnswerFilter Answer(string @operator, string key, string value) => new(key, @operator, value);

    private async Task<GridWorld> SeedAsync(bool withSchema)
    {
        await fixture.Checkpoint.ResetAsync(fixture.ConnectionString, fixture.Provider, Cancellation);
        await ReportingTestSchema.EnsureMigratedAsync(fixture.ConnectionString, fixture.Provider, Cancellation);

        await using var appDb = CreateAppDbContext();
        appDb.Set<Tenant>().Add(new Tenant("grid-search-tenant", "tnntgrds") { Id = TenantId });
        await appDb.SaveChangesAsync(Cancellation);

        var form = Form.Create(new FormCreateArgs(TenantId: TenantId, Name: "Grid search form"));
        appDb.Forms.Add(form);
        await appDb.SaveChangesAsync(Cancellation);
        FormDefinition definition = new(TenantId, isDraft: false, jsonData: """{"pages":[]}""");
        form.AddFormDefinition(definition);
        appDb.Set<FormDefinition>().Add(definition);
        await appDb.SaveChangesAsync(Cancellation);

        if (withSchema)
        {
            await using var reportingDb = CreateReportingDbContext();
            FormSchema schema = new(TenantId, form.Id, 1, FormSchema.EmptyFlatteningMapJson, FormSchema.EmptyCodebookJson);
            schema.UpdateSearchKeys(SearchKeysJson);
            reportingDb.FormSchemas.Add(schema);
            await reportingDb.SaveChangesAsync(Cancellation);
        }

        return new GridWorld(this, form.Id, definition.Id);
    }

    private AppDbContext CreateAppDbContext()
    {
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigureOptions(optionsBuilder, fixture.ConnectionString, fixture.Provider);
        return new AppDbContext(optionsBuilder.Options, new IntegrationTenantContext(TenantId), new OutboxIntegrationEventDispatcher());
    }

    private ReportingDbContextBase CreateReportingDbContext() =>
        ReportingTestSchema.CreateContext(fixture.ConnectionString, fixture.Provider, new IntegrationTenantContext(TenantId));

    // Submissions of one form, prepared the way a flatten writes them: the search copy beside the answers, stamped
    // with the submission's own version.
    private sealed class GridWorld(SubmissionGridAnswerSearchIntegrationTests tests, long formId, long formDefinitionId)
    {
        public long FormId => formId;

        public async Task<SubmissionGridPage> SearchAsync(
            IReadOnlyList<AnswerFilter> answers,
            ListByFormIdQuery? list = null,
            IReadOnlyList<string>? columns = null)
        {
            var result = await ExecuteAsync(answers, list, columns);
            result.Status.Should().Be(ResultStatus.Ok);
            return result.Value;
        }

        public async Task<Result<SubmissionGridPage>> ExecuteAsync(
            IReadOnlyList<AnswerFilter> answers,
            ListByFormIdQuery? list = null,
            IReadOnlyList<string>? columns = null)
        {
            await using var reportingDb = tests.CreateReportingDbContext();
            SubmissionGridAnswerSearch sut = new(
                reportingDb,
                SearchCopyForms.KeyFilter(tests.Provider),
                SearchCopyForms.ProfileFilter(tests.Provider));
            SubmissionGridRowsQuery query = new(TenantId, list ?? new ListByFormIdQuery(formId, 1, 10), columns ?? [], answers);

            return await sut.ExecuteAsync(query, Cancellation);
        }

        public async Task<long> PreparedAsync(string searchJson, string? profile = null, string? dataJson = null)
        {
            var id = await AddAsync(isComplete: true, profile);
            await PrepareAsync(id, searchJson, dataJson ?? searchJson);
            return id;
        }

        // Prepared, then changed: the row no longer matches the submission's version.
        public async Task<long> StaleAsync(string searchJson)
        {
            var id = await PreparedAsync(searchJson);
            await using var appDb = tests.CreateAppDbContext();
            await appDb.Submissions
                .Where(submission => submission.Id == id)
                .ExecuteUpdateAsync(updates => updates.SetProperty(s => s.ModifiedAt, DateTime.UtcNow.AddMinutes(5)), Cancellation);
            return id;
        }

        public Task<long> InProgressAsync() => AddAsync(isComplete: false, profile: null);

        private async Task<long> AddAsync(bool isComplete, string? profile)
        {
            await using var appDb = tests.CreateAppDbContext();
            var submission = Submission.Create(new SubmissionCreateArgs(
                TenantId: TenantId,
                FormId: formId,
                FormDefinitionId: formDefinitionId,
                JsonData: "{}",
                IsComplete: isComplete,
                SubmitterProfileSnapshot: profile));
            appDb.Submissions.Add(submission);
            await appDb.SaveChangesAsync(Cancellation);
            return submission.Id;
        }

        private async Task PrepareAsync(long submissionId, string searchJson, string dataJson)
        {
            await using var reportingDb = tests.CreateReportingDbContext();
            await new FlattenedRowSeed(reportingDb).ProcessedAsync(
                new FlattenedSubmissionKey(TenantId, formId, submissionId),
                new FlattenedRowData(dataJson, searchJson),
                Cancellation);

            await using var appDb = tests.CreateAppDbContext();
            var version = await appDb.Submissions
                .Where(submission => submission.Id == submissionId)
                .Select(submission => submission.ModifiedAt ?? submission.CreatedAt)
                .SingleAsync(Cancellation);
            await reportingDb.FlattenedSubmissions
                .Where(row => row.SubmissionId == submissionId)
                .ExecuteUpdateAsync(updates => updates.SetProperty(row => row.SourceModifiedAt, version), Cancellation);
        }
    }
}
