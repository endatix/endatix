using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Persistence;
using Endatix.Persistence.PostgreSql.Querying;
using Endatix.Persistence.SqlServer.Querying;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Endatix.IntegrationTests;

/// <summary>
/// SPIKE (throwaway) — last POC: current persistence plus a capped filterable set.
/// V0 scan once on DataJson (deep keys), V1 the same with the filtered keys written first, V2 / V2b a capped
/// SearchJson column (64 / 128 keys, lowercased) on the existing FlattenedSubmissions row.
/// Run with SPIKE_POC=1 SPIKE_SEARCH=1 SPIKE_SHAPE=worst|realistic|mid SPIKE_ROWS=n (SQL Server: SPIKE_JSON_MAX=1).
/// </summary>
public sealed partial class SubmissionsGridSpikeTests
{
    private sealed record PocShape(string Name, int Cols, double Fill, bool ShortKeys)
    {
        public string Key(int k) => ShortKeys ? $"q{k:D4}" : $"question{k}";

        // Planted filter columns: the same rows match in the early and the deep copy.
        public int EarlyZ => 3;
        public int EarlyK => 4;
        public int EarlyM => 5;
        public int Date => 7;
        public int Long => 20;
        public int DeepZ => Cols - 5;
        public int DeepK => Cols / 2;
        public int DeepM => Cols - 10;

        public IReadOnlyList<int> Filterable(int cap) =>
            new[] { EarlyZ, EarlyK, EarlyM, Date, Long, DeepZ, DeepK, DeepM }
                .Concat(Enumerable.Range(1, Cols))
                .Distinct()
                .Take(cap)
                .OrderBy(k => k)
                .ToList();
    }

    private static PocShape ShapeFromEnv() => (Environment.GetEnvironmentVariable("SPIKE_SHAPE") ?? "realistic") switch
    {
        "worst" => new PocShape("worst: 1,000 cols, all answered", 1000, 1.0, ShortKeys: true),
        "mid" => new PocShape("mid: 200 cols, 50% answered", 200, 0.5, ShortKeys: false),
        _ => new PocShape("realistic: 1,000 cols, 20% answered, 2% long comments", 1000, 0.2, ShortKeys: false),
    };

    private sealed record PocExpected(int Total, int Zebra, int Mango, int All3, int All3InYear, int Long);

    private sealed class PocStats
    {
        public long SearchBuildTicks;
        public long DataBuildTicks;
    }

    private IQueryable<GridRow> PocBase(ReportingDbContextBase db, long formId) =>
        from s in db.CoreSubmissionRows
        where s.FormId == formId
        join f in db.FlattenedSubmissions on s.Id equals f.SubmissionId into fj
        from f in fj.DefaultIfEmpty()
        select new GridRow
        {
            Id = s.Id,
            CreatedAt = s.CreatedAt,
            Stamp = s.ModifiedAt ?? s.CreatedAt,
            IsComplete = s.IsComplete,
            IntegrationStatus = f == null ? null : f.Integration.Code,
            SourceModifiedAt = f == null ? null : f.SourceModifiedAt,
            DataJson = f == null ? null : f.DataJson,
            SearchJson64 = f == null ? null : f.SearchJson64,
            SearchJson128 = f == null ? null : f.SearchJson128,
        };

    private IQueryable<GridRow> KeyContains(IQueryable<GridRow> q, string property, string key, string text) =>
        KeyFilter().WhereKeyMatches(q, property, key, text);

    private IQueryable<GridRow> KeyBetween(IQueryable<GridRow> q, string property, string key, string fromInclusive, string toExclusive)
    {
        var p = Expression.Parameter(typeof(GridRow), "e");
        var max = Environment.GetEnvironmentVariable("SPIKE_JSON_MAX") == "1";
        Expression extract = _fixture.Provider == TestDatabaseProvider.SqlServer
            ? Expression.Call(max ? SqlServerJsonDbFunctions.JsonValueMaxMethod : SqlServerJsonDbFunctions.JsonValueMethod,
                Expression.Property(p, property), Expression.Constant(SqlServerJsonObjectKeyFilter.BuildJsonValuePath(key)))
            : Expression.Call(NpgsqlJsonDbFunctions.ExtractObjectKeyTextMethod, Expression.Property(p, property), Expression.Constant(key));
        var compare = typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!;
        var body = Expression.AndAlso(
            Expression.GreaterThanOrEqual(Expression.Call(compare, extract, Expression.Constant(fromInclusive)), Expression.Constant(0)),
            Expression.LessThan(Expression.Call(compare, extract, Expression.Constant(toExclusive)), Expression.Constant(0)));
        return q.Where(Expression.Lambda<Func<GridRow, bool>>(body, p));
    }

    // Scan once: ids + sort key of every match, count and page in memory, the page's rows by id.
    private async Task<int> PocScanOnceAsync(Func<ReportingDbContextBase, IQueryable<GridRow>> build, CancellationToken ct)
    {
        await using var db = ReportingTestSchema.CreateContext(_fixture.ConnectionString, _fixture.Provider, new IntegrationTenantContext(TenantId));
        var keys = await build(db).AsNoTracking().Select(r => new { r.Id, r.CreatedAt }).ToListAsync(ct);
        var pageIds = keys.OrderByDescending(k => k.CreatedAt).ThenBy(k => k.Id).Take(50).Select(k => k.Id).ToList();
        var page = await db.FlattenedSubmissions.AsNoTracking().Where(f => pageIds.Contains(f.SubmissionId)).Select(f => new { f.SubmissionId, f.DataJson }).ToListAsync(ct);
        page.Count.Should().Be(pageIds.Count);
        return keys.Count;
    }

    // Metadata only, as T3 does it: count, then the page.
    private async Task<int> PocMetadataOnlyAsync(long formId, CancellationToken ct)
    {
        await using var db = ReportingTestSchema.CreateContext(_fixture.ConnectionString, _fixture.Provider, new IntegrationTenantContext(TenantId));
        var total = await PocBase(db, formId).AsNoTracking().CountAsync(ct);
        _ = await PocBase(db, formId).AsNoTracking().OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Take(50).Select(r => new { r.Id, r.DataJson }).ToListAsync(ct);
        return total;
    }

    [Fact]
    public async Task Poc_CappedFilterableSet()
    {
        var ct = TestContext.Current.CancellationToken;
        if (Environment.GetEnvironmentVariable("SPIKE_POC") != "1") { return; }
        Environment.GetEnvironmentVariable("SPIKE_SEARCH").Should().Be("1", "the SearchJson columns must be in the EF model");

        var shape = ShapeFromEnv();
        var rows = int.Parse(Environment.GetEnvironmentVariable("SPIKE_ROWS") ?? "50000", CultureInfo.InvariantCulture);
        var iterations = int.Parse(Environment.GetEnvironmentVariable("SPIKE_ITER") ?? "7", CultureInfo.InvariantCulture);
        var sql = _fixture.Provider == TestDatabaseProvider.SqlServer;
        var label = $"{_fixture.Provider} {rows / 1000}k {(Environment.GetEnvironmentVariable("SPIKE_SHAPE") ?? "realistic")}";

        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, ct);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, ct);
        await using var app = CreateAppDb(TenantId);
        await SeedTenantAsync(app, TenantId, ct);
        var (formId, defId) = await SeedFormAsync(app, TenantId, ct);
        var templateId = await SeedSubmissionAsync(app, TenantId, formId, defId, true, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), ct);
        var conn = app.Database.GetDbConnection();
        await conn.OpenAsync(ct);
        if (sql)
        {
            await ExecAsync(conn, "ALTER TABLE reporting.FlattenedSubmissions ADD SearchJson64 json NULL, SearchJson128 json NULL", ct);
        }
        else
        {
            await ExecAsync(conn, "ALTER TABLE reporting.\"FlattenedSubmissions\" ADD COLUMN \"SearchJson64\" jsonb, ADD COLUMN \"SearchJson128\" jsonb", ct);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## {label}: {shape.Name}, {rows:N0} submissions, {iterations} runs per scenario (+2 warm-up), whole request (scan once + page fetch)");
        var load = Stopwatch.StartNew();
        await CloneSubmissionsAsync(conn, templateId, rows, ct);
        var stats = new PocStats();
        var expected = sql
            ? await BulkLoadSqlServerAsync((SqlConnection)conn, formId, rows, shape, stats, ct)
            : await BulkLoadPostgreSqlAsync((NpgsqlConnection)conn, formId, rows, shape, stats, ct);
        await AnalyzeAsync(app, ct);
        sb.AppendLine($"load: {load.Elapsed.TotalSeconds:F0}s; expected: {expected}");
        sb.AppendLine($"C# build per row: DataJson {TimeSpan.FromTicks(stats.DataBuildTicks / rows).TotalMicroseconds:F0} µs, SearchJson64+128 {TimeSpan.FromTicks(stats.SearchBuildTicks / rows).TotalMicroseconds:F0} µs");
        await DescribePocStorageAsync(conn, sb, ct);

        string K(int k) => shape.Key(k);
        const string D = nameof(GridRow.DataJson), S64 = nameof(GridRow.SearchJson64), S128 = nameof(GridRow.SearchJson128);
        IQueryable<GridRow> Fresh(ReportingDbContextBase db) => FreshOnly(PocBase(db, formId));
        IQueryable<GridRow> Three(ReportingDbContextBase db, string col, int m, int k, int z) =>
            KeyContains(KeyContains(KeyContains(Fresh(db), col, K(m), "mango"), col, K(k), "kiwi"), col, K(z), "zebra");

        var scenarios = new List<(string Name, Func<Task<int>> Run, int Expected)>
        {
            ("reference: no answer filter (count + page)", () => PocMetadataOnlyAsync(formId, ct), expected.Total),

            ("V0 DataJson deep: 1 contains 1%", () => PocScanOnceAsync(db => KeyContains(Fresh(db), D, K(shape.DeepZ), "zebra"), ct), expected.Zebra),
            ("V0 DataJson deep: 1 contains 25%", () => PocScanOnceAsync(db => KeyContains(Fresh(db), D, K(shape.DeepM), "mango"), ct), expected.Mango),
            ("V0 DataJson deep: 3 contains", () => PocScanOnceAsync(db => Three(db, D, shape.DeepM, shape.DeepK, shape.DeepZ), ct), expected.All3),
            ("V0 DataJson deep: 3 contains + date", () => PocScanOnceAsync(db => KeyBetween(Three(db, D, shape.DeepM, shape.DeepK, shape.DeepZ), D, K(shape.Date), "2025-01-01", "2026-01-01"), ct), expected.All3InYear),
            ("V0 DataJson deep: no match", () => PocScanOnceAsync(db => KeyContains(Fresh(db), D, K(shape.DeepK), "qqqqqq"), ct), 0),
            ("V0 DataJson: long comment (2%)", () => PocScanOnceAsync(db => KeyContains(Fresh(db), D, K(shape.Long), "longneedle"), ct), expected.Long),

            ("V1 DataJson early keys: 1 contains 1%", () => PocScanOnceAsync(db => KeyContains(Fresh(db), D, K(shape.EarlyZ), "zebra"), ct), expected.Zebra),
            ("V1 DataJson early keys: 3 contains", () => PocScanOnceAsync(db => Three(db, D, shape.EarlyM, shape.EarlyK, shape.EarlyZ), ct), expected.All3),
            ("V1 DataJson early keys: no match", () => PocScanOnceAsync(db => KeyContains(Fresh(db), D, K(shape.EarlyK), "qqqqqq"), ct), 0),

            ("V2 SearchJson64: 1 contains 1%", () => PocScanOnceAsync(db => KeyContains(Fresh(db), S64, K(shape.DeepZ), "zebra"), ct), expected.Zebra),
            ("V2 SearchJson64: 1 contains 25%", () => PocScanOnceAsync(db => KeyContains(Fresh(db), S64, K(shape.DeepM), "mango"), ct), expected.Mango),
            ("V2 SearchJson64: 3 contains", () => PocScanOnceAsync(db => Three(db, S64, shape.DeepM, shape.DeepK, shape.DeepZ), ct), expected.All3),
            ("V2 SearchJson64: 3 contains + date", () => PocScanOnceAsync(db => KeyBetween(Three(db, S64, shape.DeepM, shape.DeepK, shape.DeepZ), S64, K(shape.Date), "2025-01-01", "2026-01-01"), ct), expected.All3InYear),
            ("V2 SearchJson64: no match", () => PocScanOnceAsync(db => KeyContains(Fresh(db), S64, K(shape.DeepK), "qqqqqq"), ct), 0),
            ("V2 SearchJson64: long comment (2%)", () => PocScanOnceAsync(db => KeyContains(Fresh(db), S64, K(shape.Long), "longneedle"), ct), expected.Long),

            ("V2b SearchJson128: 3 contains", () => PocScanOnceAsync(db => Three(db, S128, shape.DeepM, shape.DeepK, shape.DeepZ), ct), expected.All3),
            ("V2b SearchJson128: no match", () => PocScanOnceAsync(db => KeyContains(Fresh(db), S128, K(shape.DeepK), "qqqqqq"), ct), 0),
        };

        sb.AppendLine();
        sb.AppendLine("| scenario | total | p50 ms | p95 ms |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var (name, run, exp) in scenarios)
        {
            var times = new List<double>();
            int total = -1;
            for (var i = -2; i < iterations; i++)
            {
                var t = Stopwatch.GetTimestamp();
                total = await run();
                if (i >= 0) times.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds);
            }

            var ok = total == exp ? "" : $" **WRONG: got {total}**";
            sb.AppendLine($"| {name} | {exp}{ok} | {P(times, .5):F0} | {P(times, .95):F0} |");
            Log("poc." + label, sb.ToString());
        }

        // Concurrency: 4 callers at once, V2 3 contains.
        var concurrent = new List<double>();
        var concurrentV0 = new List<double>();
        async Task Callers(List<double> sink, Func<ReportingDbContextBase, IQueryable<GridRow>> build)
        {
            await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
            {
                for (var r = 0; r < 6; r++)
                {
                    var t = Stopwatch.GetTimestamp();
                    await PocScanOnceAsync(build, ct);
                    var ms = Stopwatch.GetElapsedTime(t).TotalMilliseconds;
                    if (r > 0) { lock (sink) { sink.Add(ms); } }
                }
            }));
        }

        await Callers(concurrent, db => Three(db, S64, shape.DeepM, shape.DeepK, shape.DeepZ));
        await Callers(concurrentV0, db => Three(db, D, shape.DeepM, shape.DeepK, shape.DeepZ));
        sb.AppendLine($"| 4 concurrent callers, V2 SearchJson64: 3 contains | {expected.All3} | {P(concurrent, .5):F0} | {P(concurrent, .95):F0} |");
        sb.AppendLine($"| 4 concurrent callers, V0 DataJson deep: 3 contains | {expected.All3} | {P(concurrentV0, .5):F0} | {P(concurrentV0, .95):F0} |");

        // Rebuild SearchJson64 from DataJson in SQL: what changing a form's filterable set costs.
        var cap = shape.Filterable(64);
        var rebuild = Stopwatch.StartNew();
        if (sql)
        {
            var pairs = string.Join(", ", cap.Select(k => $"'{K(k)}': LOWER(JSON_VALUE([DataJson], '$.\"{K(k)}\"' RETURNING nvarchar(max)))"));
            await ExecAsync(conn, $"UPDATE reporting.FlattenedSubmissions SET SearchJson64 = CAST(JSON_OBJECT({pairs} ABSENT ON NULL) AS json) WHERE FormId = {formId}", ct);
        }
        else
        {
            var chunks = cap.Chunk(40).Select(c => "jsonb_build_object(" + string.Join(", ", c.Select(k => $"'{K(k)}', lower(\"DataJson\" ->> '{K(k)}')")) + ")");
            await ExecAsync(conn, $"UPDATE reporting.\"FlattenedSubmissions\" SET \"SearchJson64\" = jsonb_strip_nulls({string.Join(" || ", chunks)}) WHERE \"FormId\" = {formId}", ct);
        }

        sb.AppendLine();
        sb.AppendLine($"rebuild SearchJson64 from DataJson in one SQL UPDATE ({rows:N0} rows): {rebuild.Elapsed.TotalSeconds:F1}s");

        // Sanity: the rebuilt column still answers the same.
        (await PocScanOnceAsync(db => Three(db, S64, shape.DeepM, shape.DeepK, shape.DeepZ), ct)).Should().Be(expected.All3);
        (await PocScanOnceAsync(db => KeyContains(Fresh(db), S64, K(shape.Long), "longneedle"), ct)).Should().Be(expected.Long);

        if (!sql)
        {
            await using var ex = ReportingTestSchema.CreateContext(_fixture.ConnectionString, _fixture.Provider, new IntegrationTenantContext(TenantId));
            foreach (var (name, build) in new (string, Func<ReportingDbContextBase, IQueryable<GridRow>>)[]
                     {
                         ("V0 DataJson deep: 3 contains", db => Three(db, D, shape.DeepM, shape.DeepK, shape.DeepZ)),
                         ("V2 SearchJson64: 3 contains", db => Three(db, S64, shape.DeepM, shape.DeepK, shape.DeepZ)),
                     })
            {
                var text = InlineParameters(build(ex).Select(r => new { r.Id, r.CreatedAt }).ToQueryString());
                var plan = string.Join("\n", (await QueryAsync(conn, "EXPLAIN (ANALYZE, BUFFERS) " + text, ct)).Select(r => r[0]));
                Log($"poc.explain.{label}.{name}", plan);
            }
        }

        Log("poc." + label, sb.ToString());
    }

    private async Task CloneSubmissionsAsync(DbConnection conn, long templateId, int rows, CancellationToken ct)
    {
        var sql = _fixture.Provider == TestDatabaseProvider.SqlServer;
        string Q(string n) => sql ? $"[{n}]" : $"\"{n}\"";
        const long baseId = 5_000_000_000_000L;
        var colNames = (await QueryAsync(conn, sql
            ? "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Submissions' AND TABLE_SCHEMA = 'dbo' ORDER BY ORDINAL_POSITION"
            : "SELECT column_name FROM information_schema.columns WHERE table_name = 'Submissions' AND table_schema = 'public' ORDER BY ordinal_position", ct))
            .Select(r => r[0]!.ToString()!).ToList();
        string Expr(string c) => c switch
        {
            "Id" => $"{baseId} + n.i",
            "CreatedAt" => sql ? "DATEADD(MINUTE, n.i, CAST('2026-01-01T00:00:00' AS datetime2))" : "(timestamptz '2026-01-01 00:00:00+00' + n.i * interval '1 minute')",
            "ModifiedAt" => "NULL",
            _ => $"t.{Q(c)}",
        };
        var generator = sql
            ? $"(SELECT TOP ({rows}) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS bigint) AS i FROM sys.all_objects a CROSS JOIN sys.all_objects b CROSS JOIN sys.all_objects c) n"
            : $"(SELECT generate_series(1, {rows})::bigint AS i) n";
        await ExecAsync(conn, $"INSERT INTO {Q("Submissions")} ({string.Join(", ", colNames.Select(Q))}) SELECT {string.Join(", ", colNames.Select(Expr))} FROM {Q("Submissions")} t CROSS JOIN {generator} WHERE t.{Q("Id")} = {templateId}", ct);
        await ExecAsync(conn, $"DELETE FROM {Q("Submissions")} WHERE {Q("Id")} = {templateId}", ct);
    }

    private static readonly string[] PocVocabulary = BuildVocabulary();

    // One generated row: DataJson in schema order (null for unanswered, as the flattener writes it), and the two
    // capped search documents (answered filterable keys only, lowercased).
    private static (string Data, string S64, string S128) BuildPocRow(int i, PocShape shape, IReadOnlyList<int> cap64, IReadOnlyList<int> cap128,
        ref (int Zebra, int Mango, int All3, int All3InYear, int Long) counts, PocStats stats)
    {
        var t0 = Stopwatch.GetTimestamp();
        var zebra = i % 100 == 0;
        var kiwi = i % 20 == 0;
        var mango = i % 4 == 0;
        var longComment = i % 50 == 0;
        var date = new DateTime(2025, 1, 1).AddDays(i % 730).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (zebra) counts.Zebra++;
        if (mango) counts.Mango++;
        if (zebra && kiwi && mango) { counts.All3++; if (string.CompareOrdinal(date, "2026-01-01") < 0) counts.All3InYear++; }
        if (longComment) counts.Long++;

        string W(int a) => PocVocabulary[(int)((uint)(i * 31 + a * 17) % PocVocabulary.Length)];
        string? Value(int k)
        {
            if (k == shape.Date) return date;
            if (k == shape.EarlyZ || k == shape.DeepZ) return zebra ? "A Zebra crossing " + W(k) : W(k) + " " + W(k + 1);
            if (k == shape.EarlyK || k == shape.DeepK) return kiwi ? "Kiwi " + W(k) : W(k) + " " + W(k + 1);
            if (k == shape.EarlyM || k == shape.DeepM) return mango ? "mango " + W(k) : W(k) + " " + W(k + 1);
            if (k == shape.Long && longComment)
            {
                var text = new StringBuilder();
                var target = 2000 + (i % 7) * 1000;
                for (var w = 0; text.Length < target; w++) text.Append(W(k + w)).Append(' ');
                return text.Append("LongNeedle").ToString();
            }

            var answered = shape.Fill >= 1.0 || ((uint)(i * 2654435761u ^ (uint)k * 40503u) % 1000) / 1000.0 < shape.Fill;
            return answered ? W(k) + " " + W(k + 3) : null;
        }

        var values = new string?[shape.Cols + 1];
        var data = new StringBuilder(shape.Cols * 24);
        data.Append('{');
        for (var k = 1; k <= shape.Cols; k++)
        {
            values[k] = Value(k);
            if (k > 1) data.Append(',');
            data.Append('"').Append(shape.Key(k)).Append("\":");
            if (values[k] is null) data.Append("null"); else data.Append('"').Append(values[k]).Append('"');
        }

        data.Append('}');
        var t1 = Stopwatch.GetTimestamp();

        string Search(IReadOnlyList<int> cap)
        {
            var s = new StringBuilder(cap.Count * 32);
            s.Append('{');
            var first = true;
            foreach (var k in cap)
            {
                if (values[k] is null) continue;
                if (!first) s.Append(',');
                first = false;
                s.Append('"').Append(shape.Key(k)).Append("\":\"").Append(values[k]!.ToLowerInvariant()).Append('"');
            }

            return s.Append('}').ToString();
        }

        var result = (data.ToString(), Search(cap64), Search(cap128));
        var t2 = Stopwatch.GetTimestamp();
        stats.DataBuildTicks += Stopwatch.GetElapsedTime(t0, t1).Ticks;
        stats.SearchBuildTicks += Stopwatch.GetElapsedTime(t1, t2).Ticks;
        return result;
    }

    private async Task<PocExpected> BulkLoadPostgreSqlAsync(NpgsqlConnection conn, long formId, int rows, PocShape shape, PocStats stats, CancellationToken ct)
    {
        var cap64 = shape.Filterable(64);
        var cap128 = shape.Filterable(128);
        var counts = (Zebra: 0, Mango: 0, All3: 0, All3InYear: 0, Long: 0);
        const long baseId = 5_000_000_000_000L;
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await using (var import = await conn.BeginBinaryImportAsync(
                         "COPY reporting.\"FlattenedSubmissions\" (\"SubmissionId\",\"TenantId\",\"FormId\",\"DataJson\",\"IsDeleted\",\"CreatedAt\",\"ModifiedAt\",\"SourceModifiedAt\",\"SourceRevision\",\"IntegrationStatus\",\"Integration_LastAttemptAt\",\"Integration_ProcessedAt\",\"Integration_LastError\",\"SearchJson64\",\"SearchJson128\") FROM STDIN (FORMAT BINARY)", ct))
        {
            for (var i = 1; i <= rows; i++)
            {
                var (data, s64, s128) = BuildPocRow(i, shape, cap64, cap128, ref counts, stats);
                var at = start.AddMinutes(i);
                await import.StartRowAsync(ct);
                await import.WriteAsync(baseId + i, NpgsqlDbType.Bigint, ct);
                await import.WriteAsync(TenantId, NpgsqlDbType.Bigint, ct);
                await import.WriteAsync(formId, NpgsqlDbType.Bigint, ct);
                await import.WriteAsync(data, NpgsqlDbType.Jsonb, ct);
                await import.WriteAsync(false, NpgsqlDbType.Boolean, ct);
                await import.WriteAsync(at, NpgsqlDbType.TimestampTz, ct);
                await import.WriteNullAsync(ct);
                await import.WriteAsync(at, NpgsqlDbType.TimestampTz, ct);
                await import.WriteAsync(1L, NpgsqlDbType.Bigint, ct);
                await import.WriteAsync("processed", NpgsqlDbType.Varchar, ct);
                await import.WriteAsync(at, NpgsqlDbType.TimestampTz, ct);
                await import.WriteAsync(at, NpgsqlDbType.TimestampTz, ct);
                await import.WriteNullAsync(ct);
                await import.WriteAsync(s64, NpgsqlDbType.Jsonb, ct);
                await import.WriteAsync(s128, NpgsqlDbType.Jsonb, ct);
            }

            await import.CompleteAsync(ct);
        }

        return new PocExpected(rows, counts.Zebra, counts.Mango, counts.All3, counts.All3InYear, counts.Long);
    }

    private async Task<PocExpected> BulkLoadSqlServerAsync(SqlConnection conn, long formId, int rows, PocShape shape, PocStats stats, CancellationToken ct)
    {
        var cap64 = shape.Filterable(64);
        var cap128 = shape.Filterable(128);
        var counts = (Zebra: 0, Mango: 0, All3: 0, All3InYear: 0, Long: 0);
        const long baseId = 5_000_000_000_000L;
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await ExecAsync(conn, "DROP TABLE IF EXISTS reporting.SpikeStage; CREATE TABLE reporting.SpikeStage (SubmissionId bigint NOT NULL, CreatedAt datetime2 NOT NULL, DataJson varchar(max) NOT NULL, S64 varchar(max) NOT NULL, S128 varchar(max) NOT NULL)", ct);
        const int chunk = 5000;
        for (var from = 1; from <= rows; from += chunk)
        {
            var table = new DataTable();
            table.Columns.Add("SubmissionId", typeof(long));
            table.Columns.Add("CreatedAt", typeof(DateTime));
            table.Columns.Add("DataJson", typeof(string));
            table.Columns.Add("S64", typeof(string));
            table.Columns.Add("S128", typeof(string));
            for (var i = from; i < from + chunk && i <= rows; i++)
            {
                var (data, s64, s128) = BuildPocRow(i, shape, cap64, cap128, ref counts, stats);
                table.Rows.Add(baseId + i, start.AddMinutes(i), data, s64, s128);
            }

            using (var bulk = new SqlBulkCopy(conn) { DestinationTableName = "reporting.SpikeStage", BulkCopyTimeout = 0, BatchSize = chunk })
            {
                await bulk.WriteToServerAsync(table, ct);
            }

            await ExecAsync(conn, $"""
                INSERT INTO reporting.FlattenedSubmissions (SubmissionId, TenantId, FormId, DataJson, IsDeleted, CreatedAt, ModifiedAt, SourceModifiedAt, SourceRevision, IntegrationStatus, Integration_LastAttemptAt, Integration_ProcessedAt, Integration_LastError, SearchJson64, SearchJson128)
                SELECT SubmissionId, {TenantId}, {formId}, CAST(DataJson AS json), 0, CreatedAt, NULL, CreatedAt, 1, 'processed', CreatedAt, CreatedAt, NULL, CAST(S64 AS json), CAST(S128 AS json) FROM reporting.SpikeStage;
                TRUNCATE TABLE reporting.SpikeStage;
                """, ct);
        }

        await ExecAsync(conn, "DROP TABLE reporting.SpikeStage", ct);
        return new PocExpected(rows, counts.Zebra, counts.Mango, counts.All3, counts.All3InYear, counts.Long);
    }

    private async Task DescribePocStorageAsync(DbConnection conn, StringBuilder sb, CancellationToken ct)
    {
        if (_fixture.Provider == TestDatabaseProvider.PostgreSql)
        {
            var r = (await QueryAsync(conn, """
                SELECT avg(octet_length("DataJson"::text))::int, avg(pg_column_size("DataJson"))::int,
                       avg(pg_column_size("SearchJson64"))::int, avg(pg_column_size("SearchJson128"))::int,
                       pg_size_pretty(pg_total_relation_size('reporting."FlattenedSubmissions"')),
                       count(*) FILTER (WHERE pg_column_size("SearchJson64") > 2000)
                FROM reporting."FlattenedSubmissions"
                """, ct)).Single();
            sb.AppendLine($"storage: DataJson text {r[0]} B, stored {r[1]} B; SearchJson64 {r[2]} B; SearchJson128 {r[3]} B; table total {r[4]}; SearchJson64 rows over 2 KB (TOASTed): {r[5]}");
        }
        else
        {
            var r = (await QueryAsync(conn, """
                SELECT AVG(CAST(DATALENGTH(DataJson) AS bigint)), AVG(CAST(DATALENGTH(SearchJson64) AS bigint)), AVG(CAST(DATALENGTH(SearchJson128) AS bigint)),
                       (SELECT SUM(a.total_pages) * 8 / 1024 FROM sys.partitions p JOIN sys.allocation_units a ON a.container_id = p.partition_id WHERE p.object_id = OBJECT_ID('reporting.FlattenedSubmissions'))
                FROM reporting.FlattenedSubmissions
                """, ct)).Single();
            sb.AppendLine($"storage: DataJson {r[0]} B; SearchJson64 {r[1]} B; SearchJson128 {r[2]} B; table total {r[3]} MB");
        }
    }
}
