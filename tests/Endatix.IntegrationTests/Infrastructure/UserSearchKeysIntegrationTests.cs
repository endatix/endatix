using Endatix.Core.Infrastructure.Result;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using Microsoft.EntityFrameworkCore;
using static Endatix.IntegrationTests.SearchCopyForms;

namespace Endatix.IntegrationTests;

/// <summary>
/// Search keys people add and remove on a form, against the database: the caps, the rebuild of the rows for an added
/// key, and rows that have not caught up with a key counting as not checked.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class UserSearchKeysIntegrationTests(DbIntegrationFixture fixture)
{
    private const long OtherTenantId = FormSchemaRebuildWorld.TenantId + 1;

    private readonly FormSchemaRebuildWorld _world = new(fixture);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_form_with_thirty_eligible_questions_takes_fourteen_user_keys_and_refuses_a_fifteenth()
    {
        // Arrange
        var form = await _world.SeedFlattenedFormAsync(TextQuestions(30, readOnly: 15));
        foreach (var name in ReadOnlyNames(FormSearchKeys.MaxUserKeys))
        {
            await AddAsync(form, name);
        }

        // Act
        var fifteenth = await AddAsync(form, "r015");

        // Assert
        fifteenth.Status.Should().Be(ResultStatus.Invalid);
        fifteenth.ValidationErrors.Should().ContainSingle().Which.ErrorMessage.Should().Be(FormSearchKeys.CapMessage);
        var keys = FormSearchKeys.Of(await _world.ReadSchemaAsync(form.FormId));
        (keys.Keys.Count, keys.UserKeyCount).Should().Be((44, 14));
    }

    [Fact]
    public async Task Adding_a_key_answers_before_the_rebuild_with_the_key_pending()
    {
        // Arrange — the question is read-only, so no compile makes it searchable.
        var form = await _world.SeedFlattenedFormAsync(Mixed, new { reference = "REF-Alpha" }, new { reference = "ref-beta" });

        // Act
        var added = await AddAsync(form, "reference");

        // Assert
        added.Value.Keys.Should().ContainSingle(key => key.Key == "reference").Which.Pending.Should().BeTrue();
        (await CountCheckedAsync(form, "reference")).Should().Be(0);
        (await PendingRebuildRequestsAsync()).Should().Be(1);
    }

    [Fact]
    public async Task After_a_key_is_added_and_the_rows_rebuilt_its_filter_matches()
    {
        // Arrange
        var form = await _world.SeedFlattenedFormAsync(Mixed,
            new { reference = "REF-Alpha" }, new { reference = "ref-beta" }, new { company = "Acme" });
        await AddAsync(form, "reference");

        // Act
        await RunRebuildsAsync();

        // Assert
        (await ColumnsAsync(form)).Columns.Single(column => column.Key == "reference").SearchPending.Should().BeFalse();
        (await CountCheckedMatchesAsync(form, "reference", "ALPHA")).Should().Be(1);
    }

    [Fact]
    public async Task Rows_not_rebuilt_for_an_added_key_count_as_not_checked()
    {
        // Arrange — the key is saved, but the rebuild has not reached the rows.
        var form = await _world.SeedFlattenedFormAsync(Mixed, new { reference = "REF-Alpha" }, new { reference = "ref-beta" });
        await SaveUserKeyWithoutRebuildAsync(form, "reference");

        // Act
        var checkedRows = await CountCheckedAsync(form, "reference");
        var column = (await ColumnsAsync(form)).Columns.Single(column => column.Key == "reference");

        // Assert
        checkedRows.Should().Be(0);
        (column.Searchable, column.SearchPending).Should().Be((true, true));
    }

    [Fact]
    public async Task Adding_the_key_again_finishes_a_rebuild_that_was_cut_off()
    {
        // Arrange
        var form = await _world.SeedFlattenedFormAsync(Mixed, new { reference = "REF-Alpha" }, new { reference = "ref-beta" });
        await SaveUserKeyWithoutRebuildAsync(form, "reference");

        // Act
        await AddAsync(form, "reference");
        await RunRebuildsAsync();

        // Assert
        (await ColumnsAsync(form)).Columns.Single(column => column.Key == "reference").SearchPending.Should().BeFalse();
        (await CountCheckedAsync(form, "reference")).Should().Be(2);
    }

    [Fact]
    public async Task A_flatten_after_a_key_was_added_writes_it_and_stamps_the_row_with_the_new_revision()
    {
        // Arrange
        var form = await _world.SeedFlattenedFormAsync(Mixed, new { company = "Acme" });
        await AddAsync(form, "reference");
        await RunRebuildsAsync();
        var submissionId = await _world.AddRealSubmissionAsync(form.FormId, form.DefinitionIds[0], Answers(new { reference = "REF-Gamma" }));

        // Act
        await _world.FlattenAsync(form.FormId, submissionId);

        // Assert
        var row = await _world.ReadFlattenedAsync(submissionId);
        row.SearchJson.Should().Contain("ref-gamma");
        row.SearchKeysRevision.Should().Be((await _world.ReadSchemaAsync(form.FormId)).SearchKeysRevision);
    }

    [Fact]
    public async Task Removing_a_user_key_stops_filtering_on_it_at_once()
    {
        // Arrange
        var form = await _world.SeedFlattenedFormAsync(Mixed, new { reference = "REF-Alpha" });
        await AddAsync(form, "reference");

        // Act
        Result<SubmissionGridSearchKeys> removed;
        await using (var scope = _world.OpenScope())
        {
            removed = await scope.RemoveSearchKey().Handle(new RemoveSearchKeyCommand(Target(form, "reference")), Cancellation);
        }

        // Assert
        removed.Status.Should().Be(ResultStatus.Ok);
        SearchKeyNames(await _world.ReadSchemaAsync(form.FormId)).Should().NotContain("reference");
        (await ColumnsAsync(form)).Columns.Single(column => column.Key == "reference").Searchable.Should().BeFalse();
    }

    [Fact]
    public async Task Another_tenant_cannot_change_the_searchable_set()
    {
        // Arrange
        var form = await _world.SeedFlattenedFormAsync(Mixed, new { reference = "REF-Alpha" });
        await AddAsync(form, "reference");
        var before = await _world.ReadSchemaAsync(form.FormId);
        var requestsBefore = await PendingRebuildRequestsAsync();

        // Act
        Result<SubmissionGridSearchKeys> added;
        Result<SubmissionGridSearchKeys> removed;
        await using (var scope = _world.OpenScope())
        {
            added = await scope.AddSearchKey().Handle(new AddSearchKeyCommand(Target(form, "company", OtherTenantId)), Cancellation);
            removed = await scope.RemoveSearchKey().Handle(new RemoveSearchKeyCommand(Target(form, "reference", OtherTenantId)), Cancellation);
        }

        // Assert
        (added.Status, removed.Status).Should().Be((ResultStatus.NotFound, ResultStatus.NotFound));
        var after = await _world.ReadSchemaAsync(form.FormId);
        (after.SearchKeysRevision, SearchKeyNames(after)).Should().BeEquivalentTo((before.SearchKeysRevision, SearchKeyNames(before)));
        (await PendingRebuildRequestsAsync()).Should().Be(requestsBefore);
    }

    [Fact]
    public async Task A_compile_gives_a_user_key_on_a_read_only_question_its_new_filter()
    {
        // Arrange — the read-only text question becomes a read-only date question.
        var form = await _world.SeedFlattenedFormAsync(Mixed, new { reference = "2026-03-01T10:30" });
        await AddAsync(form, "reference");
        var before = await _world.ReadSchemaAsync(form.FormId);
        await _world.EditDefinitionAsync(form.DefinitionIds[0], Mixed.Replace(
            "\"name\": \"reference\", \"readOnly\": true", "\"name\": \"reference\", \"inputType\": \"date\", \"readOnly\": true", StringComparison.Ordinal));

        // Act
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);

        // Assert
        var keys = FormSearchKeys.Of(await _world.ReadSchemaAsync(form.FormId));
        keys.Find("reference")!.FilterType.Should().Be(SubmissionGridFilterTypes.Date);
        keys.Revision.Should().BeGreaterThan(before.SearchKeysRevision);
    }

    [Fact]
    public async Task A_compile_drops_a_user_key_whose_question_no_filter_supports_any_more()
    {
        // Arrange — the read-only text question becomes a read-only number question.
        var form = await _world.SeedFlattenedFormAsync(Mixed, new { reference = "REF-Alpha" });
        await AddAsync(form, "reference");
        await _world.EditDefinitionAsync(form.DefinitionIds[0], Mixed.Replace(
            "\"name\": \"reference\", \"readOnly\": true", "\"name\": \"reference\", \"inputType\": \"number\", \"readOnly\": true", StringComparison.Ordinal));

        // Act
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);

        // Assert
        SearchKeyNames(await _world.ReadSchemaAsync(form.FormId)).Should().NotContain("reference");
    }

    private static SearchKeyTarget Target(RebuildForm form, string key, long tenantId = FormSchemaRebuildWorld.TenantId) =>
        new(tenantId, form.FormId, key);

    private async Task<Result<SubmissionGridSearchKeys>> AddAsync(RebuildForm form, string key)
    {
        await using var scope = _world.OpenScope();
        return await scope.AddSearchKey().Handle(new AddSearchKeyCommand(Target(form, key)), Cancellation);
    }

    private async Task RunRebuildsAsync()
    {
        await using var scope = _world.OpenScope();
        await scope.RunSearchCopyRebuildsAsync();
    }

    private async Task<int> PendingRebuildRequestsAsync()
    {
        await using var scope = _world.OpenScope();
        return await scope.CountSearchCopyRebuildRequestsAsync();
    }

    private async Task SaveUserKeyWithoutRebuildAsync(RebuildForm form, string key)
    {
        await using var scope = _world.OpenScope();
        var saved = await scope.UserSearchKeys().ChangeAsync(
            Target(form, key),
            schema => FormSearchKeys.Of(schema).AddUser(SearchableColumns.ChosenColumn(schema, key)!, DateTime.UtcNow),
            Cancellation);
        saved.Status.Should().Be(ResultStatus.Ok);
    }

    private async Task<SubmissionGridColumns> ColumnsAsync(RebuildForm form)
    {
        await using var scope = _world.OpenScope();
        var result = await scope.GridColumns().ExecuteAsync(
            new SubmissionGridColumnsQuery(FormSchemaRebuildWorld.TenantId, form.FormId, "default"), Cancellation);
        return result.Value;
    }

    private Task<int> CountCheckedAsync(RebuildForm form, string key) => CountAsync(form, key, rows => rows);

    private Task<int> CountCheckedMatchesAsync(RebuildForm form, string key, string pattern) =>
        CountAsync(form, key, rows => KeyFilter(fixture.Provider).WhereKeyMatches(rows, nameof(FlattenedSubmission.SearchJson), key, pattern));

    // The rows a filter on the key can check, narrowed by the filter itself.
    private async Task<int> CountAsync(
        RebuildForm form,
        string key,
        Func<IQueryable<FlattenedSubmission>, IQueryable<FlattenedSubmission>> filter)
    {
        var needed = FormSearchKeys.Of(await _world.ReadSchemaAsync(form.FormId)).RevisionNeededFor([key]);
        await using var db = _world.CreateReportingDbContext();
        var rows = db.FlattenedSubmissions.AsNoTracking().Where(row => row.FormId == form.FormId).HoldingSearchKeys(needed);
        return await filter(rows).CountAsync(Cancellation);
    }
}
