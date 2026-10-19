using System.Diagnostics;
using System.Text.Json;
using Endatix.Infrastructure.Data.Querying;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

/// <summary>
/// Measures answer filters on the search copy at scale: one form with tens of thousands of processed rows, filtered
/// through the real key filter on <see cref="FlattenedSubmission.SearchJson"/> and read once (the ids and sort key of
/// every match, the count and the page from that list, then the page's rows by id).
/// </summary>
/// <remarks>
/// Opt-in (<see cref="PerformanceRun"/>): it loads 50,000 rows by default and takes minutes, longer on SQL Server.
/// It reads the flattened rows only; the grid's join with the core submissions (#1255) adds its own cost, which the
/// metadata-only reference row shows once that join is measured here too.
/// </remarks>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", PerformanceRun.Category)]
public sealed class SubmissionsGridPerformanceTests(DbIntegrationFixture fixture)
{
    private const int PageSize = 50;
    private const int ConcurrentCallers = 4;
    private const string AllThreeName = "3 contains (deep)";
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly FormSchemaRebuildWorld _world = new(fixture);
    private readonly GridPerformanceShape _shape = GridPerformanceShape.FromName(PerformanceRun.ShapeName);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private IRelationalJsonObjectKeyFilter Filter => SearchCopyForms.KeyFilter(fixture.Provider);

    [Fact]
    public async Task Answer_filters_on_the_search_copy_at_scale()
    {
        Assert.SkipUnless(PerformanceRun.Enabled, PerformanceRun.SkipReason);

        // Arrange
        GridPerformanceReport report = new($"{fixture.Provider}_{PerformanceRun.Rows / 1000}k_{_shape.Name}");
        var (formId, counts) = await LoadAsync(report);

        // Act
        await MeasureSingleCallerAsync(report, formId, counts);
        await MeasureConcurrentCallersAsync(report, formId, counts);
        await MeasureRebuildAsync(report, formId, counts);
        await ExplainAsync(report, formId);

        // Assert
        report.Write();
        report.Wrong.Should().BeEmpty("every scenario must return the rows the generator planted");
    }

    private async Task<(long FormId, GridPerformanceCounts Counts)> LoadAsync(GridPerformanceReport report)
    {
        var form = await _world.SeedFormAsync([_shape.Definition()]);
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);
        var searchKeys = await AddUserKeysAsync(form.FormId);

        await using var db = _world.CreateReportingDbContext();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(Cancellation);
        var load = Stopwatch.StartNew();
        GridPerformanceLoader loader = new(_shape, searchKeys, form.FormId);
        await loader.LoadAsync(connection, PerformanceRun.Rows, Cancellation);
        await GridPerformanceSql.AnalyzeAsync(connection, fixture.Provider, Cancellation);

        report.Header(_shape, searchKeys, FormattableString.Invariant($"load {load.Elapsed.TotalSeconds:F0} s; planted: {loader.Counts}"));
        report.Line("storage: " + await GridPerformanceSql.StorageAsync(connection, fixture.Provider, Cancellation));
        return (form.FormId, loader.Counts);
    }

    // The compile chose the automatic keys; the deep planted questions fill the slots people choose, up to the cap.
    private async Task<FormSearchKeys> AddUserKeysAsync(long formId)
    {
        var schema = await _world.ReadSchemaAsync(formId);
        var automatic = FormSearchKeys.FromJson(schema.SearchKeys).Keys;
        var users = _shape.UserKeys(automatic.Select(key => key.Key).ToList())
            .Select(key => new SearchKey(key, SearchKeySources.User, SubmissionGridFilterTypes.Text, DateTime.UtcNow));
        var json = JsonSerializer.Serialize(automatic.Concat(users), WebJson);

        await using var db = _world.CreateReportingDbContext();
        await db.FormSchemas.Where(row => row.FormId == formId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.SearchKeys, json), Cancellation);
        return FormSearchKeys.FromJson(json);
    }

    private async Task MeasureSingleCallerAsync(GridPerformanceReport report, long formId, GridPerformanceCounts counts)
    {
        report.Table("One caller");
        report.Row(await MeasureAsync("no answer filter (count + page)", counts.Rows, () => MetadataOnlyAsync(formId)));
        foreach (var scenario in Scenarios(counts))
        {
            report.Row(await MeasureAsync(scenario.Name, scenario.Expected, () => ScanOnceAsync(formId, scenario.Query)));
        }
    }

    private async Task MeasureConcurrentCallersAsync(GridPerformanceReport report, long formId, GridPerformanceCounts counts)
    {
        report.Table($"{ConcurrentCallers} concurrent callers");
        foreach (var scenario in Scenarios(counts).Where(scenario => scenario.Concurrent))
        {
            report.Row(await MeasureConcurrentlyAsync(formId, scenario));
        }
    }

    private async Task MeasureRebuildAsync(GridPerformanceReport report, long formId, GridPerformanceCounts counts)
    {
        var rebuild = Stopwatch.StartNew();
        await using (var db = _world.CreateReportingDbContext())
        {
            await ReportingTestSchema.SearchCopyRebuilderFor(db).RebuildFormAsync(FormSchemaRebuildWorld.TenantId, formId, Cancellation);
        }

        report.Line(FormattableString.Invariant($"rebuild of every search copy from DataJson in SQL ({counts.Rows:N0} rows): {rebuild.Elapsed.TotalSeconds:F1} s"));
        var allThree = Scenarios(counts).Single(scenario => scenario.Name == AllThreeName);
        report.Row(await MeasureAsync("after the rebuild: " + allThree.Name, allThree.Expected, () => ScanOnceAsync(formId, allThree.Query)));
    }

    private async Task ExplainAsync(GridPerformanceReport report, long formId)
    {
        if (fixture.Provider != TestDatabaseProvider.PostgreSql)
        {
            return;
        }

        await using var db = _world.CreateReportingDbContext();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(Cancellation);
        var query = AllThree(Processed(db, formId)).Select(row => new { row.SubmissionId, row.CreatedAt }).ToQueryString();
        report.Attach("explain", await GridPerformanceSql.ExplainAsync(connection, query, Cancellation));
    }

    private IEnumerable<GridScenario> Scenarios(GridPerformanceCounts counts)
    {
        yield return new("1 contains, 1% (early key)", counts.Zebra, query => Contains(query, GridPerformanceShape.EarlyZebra, "zebra"));
        yield return new("1 contains, 1% (deep)", counts.Zebra, query => Contains(query, _shape.DeepZebra, "ZEBRA"), Concurrent: true);
        yield return new("1 contains, 25% (deep)", counts.Mango, query => Contains(query, _shape.DeepMango, "mango"));
        yield return new("2 contains (deep)", counts.MangoAndKiwi, query => Contains(Contains(query, _shape.DeepMango, "mango"), _shape.DeepKiwi, "kiwi"));
        yield return new(AllThreeName, counts.AllThree, AllThree, Concurrent: true);
        yield return new("3 contains + date range", counts.AllThreeIn2025, query => In2025(AllThree(query)), Concurrent: true);
        yield return new("long comment (2%)", counts.LongComments, query => Contains(query, GridPerformanceShape.LongComment, "longneedle"));
        yield return new("no match (full scan)", 0, query => Contains(query, _shape.DeepKiwi, "qqqqqq"));
    }

    private IQueryable<FlattenedSubmission> AllThree(IQueryable<FlattenedSubmission> query) =>
        Contains(Contains(Contains(query, _shape.DeepMango, "mango"), _shape.DeepKiwi, "kiwi"), _shape.DeepZebra, "zebra");

    private IQueryable<FlattenedSubmission> Contains(IQueryable<FlattenedSubmission> query, int question, string text) =>
        Filter.WhereKeyMatches(query, nameof(FlattenedSubmission.SearchJson), _shape.Key(question), text);

    private IQueryable<FlattenedSubmission> In2025(IQueryable<FlattenedSubmission> query)
    {
        JsonObjectKeyPath date = new(nameof(FlattenedSubmission.SearchJson), _shape.Key(GridPerformanceShape.Date));
        return Filter.WhereKeyBefore(Filter.WhereKeyAtLeast(query, date, "2025-01-01"), date, "2026-01-01");
    }

    private static IQueryable<FlattenedSubmission> Processed(ReportingDbContextBase db, long formId) =>
        db.FlattenedSubmissions.AsNoTracking().Where(row =>
            row.FormId == formId && !row.IsDeleted && row.DataJson != null &&
            row.Integration.Code == SubmissionIntegrationStatusCodes.Processed);

    // Scan once: the ids and sort key of every match, the count and the page from them, then the page's rows by id.
    private async Task<int> ScanOnceAsync(long formId, Func<IQueryable<FlattenedSubmission>, IQueryable<FlattenedSubmission>> filter)
    {
        await using var db = _world.CreateReportingDbContext();
        var matches = await filter(Processed(db, formId)).Select(row => new { row.SubmissionId, row.CreatedAt }).ToListAsync(Cancellation);
        var pageIds = matches.OrderByDescending(row => row.CreatedAt).ThenBy(row => row.SubmissionId).Take(PageSize)
            .Select(row => row.SubmissionId).ToList();
        var page = await db.FlattenedSubmissions.AsNoTracking().Where(row => pageIds.Contains(row.SubmissionId))
            .Select(row => new { row.SubmissionId, row.DataJson }).ToListAsync(Cancellation);
        page.Should().HaveCount(pageIds.Count);
        return matches.Count;
    }

    // Without answer filters the list stays two statements: the count, then the page.
    private async Task<int> MetadataOnlyAsync(long formId)
    {
        await using var db = _world.CreateReportingDbContext();
        var total = await Processed(db, formId).CountAsync(Cancellation);
        _ = await Processed(db, formId).OrderByDescending(row => row.CreatedAt).ThenBy(row => row.SubmissionId).Take(PageSize)
            .Select(row => new { row.SubmissionId, row.DataJson }).ToListAsync(Cancellation);
        return total;
    }

    private static async Task<GridMeasurement> MeasureAsync(string name, int expected, Func<Task<int>> run)
    {
        List<double> times = [];
        var total = -1;
        for (var iteration = -2; iteration < PerformanceRun.Iterations; iteration++)
        {
            var started = Stopwatch.GetTimestamp();
            total = await run();
            if (iteration >= 0)
            {
                times.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
        }

        return new GridMeasurement(name, expected, total, times);
    }

    private async Task<GridMeasurement> MeasureConcurrentlyAsync(long formId, GridScenario scenario)
    {
        List<double> times = [];
        var totals = await Task.WhenAll(Enumerable.Range(0, ConcurrentCallers).Select(async _ =>
        {
            var total = -1;
            for (var request = 0; request <= PerformanceRun.Iterations; request++)
            {
                var started = Stopwatch.GetTimestamp();
                total = await ScanOnceAsync(formId, scenario.Query);
                RecordAfterWarmUp(times, request, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }

            return total;
        }));

        return new GridMeasurement(scenario.Name, scenario.Expected, totals.Distinct().Single(), times);
    }

    private static void RecordAfterWarmUp(List<double> times, int request, double milliseconds)
    {
        if (request == 0)
        {
            return;
        }

        lock (times)
        {
            times.Add(milliseconds);
        }
    }

    private sealed record GridScenario(
        string Name,
        int Expected,
        Func<IQueryable<FlattenedSubmission>, IQueryable<FlattenedSubmission>> Query,
        bool Concurrent = false);
}
