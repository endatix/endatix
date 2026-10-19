using Endatix.Infrastructure.Data.Querying;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Domain;
using Microsoft.EntityFrameworkCore;
using static Endatix.IntegrationTests.SearchCopyForms;

namespace Endatix.IntegrationTests;

/// <summary>
/// The search copy against the database: which questions a compile makes searchable, what a flatten writes, and the
/// key filter reading it.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class SearchCopyIntegrationTests(DbIntegrationFixture fixture)
{
    private readonly FormSchemaRebuildWorld _world = new(fixture);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_form_with_thirty_eligible_questions_has_them_all_searchable()
    {
        // Arrange
        var form = await _world.SeedFormAsync([TextQuestions(30)]);

        // Act
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);

        // Assert
        SearchKeyNames(await _world.ReadSchemaAsync(form.FormId)).Should().Equal(QuestionNames(30));
    }

    [Fact]
    public async Task A_form_with_three_hundred_eligible_questions_has_the_first_fifty_searchable()
    {
        // Arrange
        var form = await _world.SeedFormAsync([TextQuestions(300)]);

        // Act
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);

        // Assert
        SearchKeyNames(await _world.ReadSchemaAsync(form.FormId)).Should().Equal(QuestionNames(50));
    }

    [Fact]
    public async Task Read_only_and_unfilterable_questions_are_not_searchable()
    {
        // Arrange
        var form = await _world.SeedFormAsync([Mixed]);

        // Act
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);

        // Assert
        SearchKeyNames(await _world.ReadSchemaAsync(form.FormId)).Should().Equal("company", "notes", "size", "visit", "arrival");
    }

    [Fact]
    public async Task A_compile_that_changes_nothing_leaves_the_search_copies_alone()
    {
        // Arrange
        var form = await FlattenedFormAsync(new { company = "Acme Corp" });
        await using (var db = _world.CreateReportingDbContext())
        {
            await db.FlattenedSubmissions.Where(row => row.FormId == form.FormId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.SearchJson, "{}"), Cancellation);
        }

        // Act
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);

        // Assert
        await using var check = _world.CreateReportingDbContext();
        var copies = await check.FlattenedSubmissions.AsNoTracking().Where(row => row.FormId == form.FormId)
            .Select(row => row.SearchJson).ToListAsync(Cancellation);
        copies.Should().Equal("{}");
    }

    [Fact]
    public async Task A_replace_that_drops_a_question_drops_its_search_key()
    {
        // Arrange
        var withoutNotes = Mixed.Replace("""{ "type": "comment", "name": "notes" },""", string.Empty, StringComparison.Ordinal);
        var form = await _world.SeedFormAsync([Mixed, withoutNotes]);
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);

        // Act
        await _world.ReplaceAsync(form.FormId, form.DefinitionIds[1]);

        // Assert
        SearchKeyNames(await _world.ReadSchemaAsync(form.FormId)).Should().Equal("company", "size", "visit", "arrival");
    }

    [Fact]
    public async Task A_flatten_writes_the_answered_search_keys_normalised_beside_the_answers()
    {
        // Arrange
        var form = await _world.SeedFormAsync([Mixed]);
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);
        var submissionId = await _world.AddRealSubmissionAsync(form.FormId, form.DefinitionIds[0], Answers(new
        {
            company = "Acme Corp",
            size = 2,
            arrival = "2026-03-01T10:30",
            reference = "REF-1",
        }));

        // Act
        await _world.FlattenAsync(form.FormId, submissionId);

        // Assert
        var row = await _world.ReadFlattenedAsync(submissionId);
        row.DataJson.Should().Contain("Acme Corp");
        JsonElementsOf(row.SearchJson!).Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["company"] = "\"acme corp\"",
            ["size"] = "\"2\"",
            ["arrival"] = "\"2026-03-01T10:30:00\"",
        });
    }

    [Fact]
    public async Task Contains_on_the_search_copy_ignores_case()
    {
        // Arrange
        var form = await FlattenedFormAsync(
            new { company = "Acme Corp" },
            new { company = "ACME Ltd" },
            new { company = "Фирма Acme" },
            new { company = "Other" });

        // Act
        var matches = await CountAsync(form, query => Filter.WhereKeyMatches(query, nameof(FlattenedSubmission.SearchJson), "company", "aCmE"));

        // Assert
        matches.Should().Be(3);
    }

    [Fact]
    public async Task Contains_matches_a_comment_of_six_thousand_characters()
    {
        // Arrange
        var comment = new string('x', 6000) + " NeedleAtTheEnd";
        var form = await FlattenedFormAsync(new { notes = comment }, new { notes = "short" });

        // Act
        var matches = await CountAsync(form, query => Filter.WhereKeyMatches(query, nameof(FlattenedSubmission.SearchJson), "notes", "NEEDLEATTHEEND"));

        // Assert
        matches.Should().Be(1);
    }

    [Fact]
    public async Task A_date_range_keeps_whole_calendar_days()
    {
        // Arrange
        var form = await FlattenedFormAsync(
            new { visit = "2026-02-28", arrival = "2026-02-28T23:59" },
            new { visit = "2026-03-01", arrival = "2026-03-01T00:00" },
            new { visit = "2026-03-31", arrival = "2026-03-31T23:59:59" },
            new { visit = "2026-04-01", arrival = "2026-04-01T00:00" });
        JsonObjectKeyPath visit = new(nameof(FlattenedSubmission.SearchJson), "visit");
        JsonObjectKeyPath arrival = new(nameof(FlattenedSubmission.SearchJson), "arrival");

        // Act
        var dates = await CountAsync(form, query => Filter.WhereKeyBefore(Filter.WhereKeyAtLeast(query, visit, "2026-03-01"), visit, "2026-04-01"));
        var dateTimes = await CountAsync(form, query => Filter.WhereKeyBefore(Filter.WhereKeyAtLeast(query, arrival, "2026-03-01"), arrival, "2026-04-01"));

        // Assert
        dates.Should().Be(2);
        dateTimes.Should().Be(2);
    }

    private IRelationalJsonObjectKeyFilter Filter => KeyFilter(fixture.Provider);

    private Task<RebuildForm> FlattenedFormAsync(params object[] answers) => _world.SeedFlattenedFormAsync(Mixed, answers);

    private async Task<int> CountAsync(RebuildForm form, Func<IQueryable<FlattenedSubmission>, IQueryable<FlattenedSubmission>> filter)
    {
        await using var db = _world.CreateReportingDbContext();
        return await filter(db.FlattenedSubmissions.AsNoTracking().Where(row => row.FormId == form.FormId))
            .CountAsync(Cancellation);
    }

    private static Dictionary<string, string> JsonElementsOf(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetRawText());
    }
}
