using System.Text.Json;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Microsoft.EntityFrameworkCore;
using static Endatix.IntegrationTests.SearchCopyForms;

namespace Endatix.IntegrationTests;

/// <summary>
/// Rebuilding a form's search copies from the stored answers in SQL: it writes what a flatten writes, and leaves the
/// answers alone.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class SearchCopyRebuildIntegrationTests(DbIntegrationFixture fixture)
{
    private readonly FormSchemaRebuildWorld _world = new(fixture);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Every shape the normalisation handles, so the SQL rebuild and the flatten must agree on each.
    private static readonly object[] Answers =
    [
        new { company = "Acme Corp", notes = "He said \"hi\" \\o/ — ФИРМА", size = 2, visit = "2026-03-01", arrival = "2026-03-01T10:30" },
        new { company = "ÉCOLE", visit = "2026-03-01T08:00:00Z", arrival = "2026-03-01 10:30:45.120+02:00" },
        new { notes = new string('Y', 6000), arrival = "2026-03-01", visit = "1 March" },
        new { company = "tab\there", arrival = "soon" },
        new { reference = "only read-only answered" },
    ];

    [Fact]
    public async Task A_rebuild_in_batches_writes_what_the_flatten_wrote_and_leaves_the_answers_unchanged()
    {
        // Arrange
        var form = await FlattenAllAsync();
        var flattened = await ReadRowsAsync(form.FormId);
        await ClearSearchCopiesAsync(form.FormId);

        // Act
        var rebuilt = await RebuildAsync(form.FormId);

        // Assert
        rebuilt.Should().Be(Answers.Length);
        var afterRebuild = await ReadRowsAsync(form.FormId);
        afterRebuild.Select(row => row.DataJson).Should().Equal(flattened.Select(row => row.DataJson));
        afterRebuild.Select(row => Normalized(row.SearchJson)).Should().Equal(flattened.Select(row => Normalized(row.SearchJson)));
    }

    [Fact]
    public async Task A_rebuild_leaves_other_forms_and_other_tenants_alone()
    {
        // Arrange — another form of the tenant, and another tenant's row under the rebuilt form's id.
        const long otherTenantId = FormSchemaRebuildWorld.TenantId + 1;
        var form = await FlattenAllAsync();
        var otherForm = await _world.AddFormAsync([Mixed]);
        await _world.CompileAsync(otherForm.FormId, otherForm.DefinitionIds[0]);
        var otherFormSubmission = await _world.AddRealSubmissionAsync(
            otherForm.FormId, otherForm.DefinitionIds[0], SearchCopyForms.Answers(new { company = "Other Form Co" }));
        await _world.FlattenAsync(otherForm.FormId, otherFormSubmission);
        await SeedOtherTenantRowAsync(new FlattenedSubmissionKey(otherTenantId, form.FormId, SubmissionId: 9_000_000_001L));
        await MarkUntouchedCopiesAsync(form.FormId, otherForm.FormId);
        await ClearSearchCopiesAsync(form.FormId);
        var untouchedBefore = await ReadUntouchedCopiesAsync(form.FormId, otherForm.FormId);

        // Act
        await RebuildAsync(form.FormId);

        // Assert
        (await ReadUntouchedCopiesAsync(form.FormId, otherForm.FormId)).Should().Equal(untouchedBefore);
        untouchedBefore.Should().HaveCount(2, "the other form's row and the other tenant's row are both seeded");
        untouchedBefore.Should().OnlyContain(copy => copy != null && copy.Contains(UntouchedMarker));
    }

    [Fact]
    public async Task A_compile_that_makes_existing_columns_searchable_rebuilds_the_rows()
    {
        // Arrange — rows flattened before the form had search keys, as after an upgrade.
        var form = await FlattenAllAsync();
        var flattened = await ReadRowsAsync(form.FormId);
        await using (var db = _world.CreateReportingDbContext())
        {
            await db.FormSchemas.Where(schema => schema.FormId == form.FormId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(schema => schema.SearchKeys, FormSchema.EmptySearchKeysJson), Cancellation);
        }

        await ClearSearchCopiesAsync(form.FormId);

        // Act
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);

        // Assert
        SearchKeyNames(await _world.ReadSchemaAsync(form.FormId)).Should().Equal("company", "notes", "size", "visit", "arrival");
        (await ReadRowsAsync(form.FormId)).Select(row => Normalized(row.SearchJson))
            .Should().Equal(flattened.Select(row => Normalized(row.SearchJson)));
    }

    [Fact]
    public async Task A_compile_after_a_rebuild_that_did_not_finish_runs_it_again()
    {
        // Arrange — the keys moved to a new revision, but the rebuild that should follow never ran.
        var form = await FlattenAllAsync();
        var flattened = await ReadRowsAsync(form.FormId);
        await ClearSearchCopiesAsync(form.FormId);

        // Act — the definition has not changed, so the compile leaves the schema as it is.
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);

        // Assert
        var revision = (await _world.ReadSchemaAsync(form.FormId)).SearchKeysRevision;
        var rows = await ReadRowsAsync(form.FormId);
        rows.Should().OnlyContain(row => row.SearchKeysRevision == revision);
        rows.Select(row => Normalized(row.SearchJson)).Should().Equal(flattened.Select(row => Normalized(row.SearchJson)));
    }

    private async Task<RebuildForm> FlattenAllAsync()
    {
        var form = await _world.SeedFormAsync([Mixed]);
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);
        foreach (var answer in Answers)
        {
            var submissionId = await _world.AddRealSubmissionAsync(form.FormId, form.DefinitionIds[0], SearchCopyForms.Answers(answer));
            await _world.FlattenAsync(form.FormId, submissionId);
        }

        return form;
    }

    private async Task<int> RebuildAsync(long formId)
    {
        await using var db = _world.CreateReportingDbContext();
        SearchCopyRebuilder rebuilder = new(db, new FormSchemaRepository(db, new ReportingUnitOfWork(db), ReportingTestSchema.TransactionLockFor(db)),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SearchCopyRebuilder>.Instance) { BatchSize = 2 };
        return await rebuilder.RebuildFormAsync(FormSchemaRebuildWorld.TenantId, formId, Cancellation);
    }

    private async Task SeedOtherTenantRowAsync(FlattenedSubmissionKey key)
    {
        await using var db = ReportingTestSchema.CreateContext(fixture.ConnectionString, fixture.Provider, new IntegrationTenantContext(key.TenantId));
        await new FlattenedRowSeed(db).ProcessedAsync(
            key,
            new FlattenedRowData("""{"company":"Other Tenant"}""", """{"company":"other tenant"}""", searchKeysRevision: 0),
            Cancellation);
    }

    // A copy no rebuild would write, so a rebuild that reached these rows would show.
    private const string UntouchedMarker = "not rebuilt";

    private async Task MarkUntouchedCopiesAsync(long formId, long otherFormId)
    {
        await using var db = ReportingTestSchema.CreateContext(fixture.ConnectionString, fixture.Provider, IntegrationTenantContext.Bypass);
        await db.FlattenedSubmissions.IgnoreQueryFilters()
            .Where(row => row.FormId == otherFormId || (row.FormId == formId && row.TenantId != FormSchemaRebuildWorld.TenantId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.SearchJson, $$"""{"company":"{{UntouchedMarker}}"}"""), Cancellation);
    }

    // The copies the rebuild of formId must not touch: every other form's, and other tenants' rows under formId.
    private async Task<List<string?>> ReadUntouchedCopiesAsync(long formId, long otherFormId)
    {
        await using var db = ReportingTestSchema.CreateContext(fixture.ConnectionString, fixture.Provider, IntegrationTenantContext.Bypass);
        return await db.FlattenedSubmissions.IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.FormId == otherFormId || (row.FormId == formId && row.TenantId != FormSchemaRebuildWorld.TenantId))
            .OrderBy(row => row.SubmissionId)
            .Select(row => row.SearchJson)
            .ToListAsync(Cancellation);
    }

    // As if the keys changed after the rows were written: the copies are empty and the schema's revision is newer.
    private async Task ClearSearchCopiesAsync(long formId)
    {
        await using var db = _world.CreateReportingDbContext();
        await db.FlattenedSubmissions.Where(row => row.FormId == formId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.SearchJson, "{}"), Cancellation);
        await db.FormSchemas.Where(schema => schema.FormId == formId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(schema => schema.SearchKeysRevision, schema => schema.SearchKeysRevision + 1), Cancellation);
    }

    private async Task<List<FlattenedSubmission>> ReadRowsAsync(long formId)
    {
        await using var db = _world.CreateReportingDbContext();
        return await db.FlattenedSubmissions.AsNoTracking()
            .Where(row => row.FormId == formId)
            .OrderBy(row => row.SubmissionId)
            .ToListAsync(Cancellation);
    }

    // Key order and whitespace differ between the providers' JSON, so documents compare by their sorted values.
    private static string Normalized(string? json)
    {
        if (json is null)
        {
            return "null";
        }

        using var document = JsonDocument.Parse(json);
        var properties = document.RootElement.EnumerateObject()
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => $"{property.Name}={ValueText(property.Value)}");
        return string.Join("|", properties);
    }

    private static string ValueText(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? "s:" + value.GetString() : value.GetRawText();
}
