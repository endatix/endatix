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

    private async Task ClearSearchCopiesAsync(long formId)
    {
        await using var db = _world.CreateReportingDbContext();
        await db.FlattenedSubmissions.Where(row => row.FormId == formId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.SearchJson, "{}"), Cancellation);
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
