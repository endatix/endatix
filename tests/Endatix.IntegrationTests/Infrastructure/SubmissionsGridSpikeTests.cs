using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Querying;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Endatix.Persistence.PostgreSql.Querying;
using Endatix.Persistence.SqlServer.Querying;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Endatix.IntegrationTests;

/// <summary>SPIKE (throwaway): de-risks the reporting submissions grid plan (endatix#1255 / #1091).</summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
public sealed class SubmissionsGridSpikeTests
{
    // Random ids: a SQL Server Respawn reset can leave an earlier test's tenant behind.
    private readonly long TenantId = Random.Shared.NextInt64(10_000, 900_000);
    private readonly long OtherTenantId = Random.Shared.NextInt64(900_001, 990_000);

    private readonly DbIntegrationFixture _fixture;

    public SubmissionsGridSpikeTests(DbIntegrationFixture fixture) => _fixture = fixture;

    // ---------------------------------------------------------------- projection used by the grid query

    public sealed class GridRow
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime Stamp { get; set; }
        public bool IsComplete { get; set; }
        public string? IntegrationStatus { get; set; }
        public DateTime? SourceModifiedAt { get; set; }
        public string? DataJson { get; set; }
    }

    private IRelationalJsonObjectKeyFilter KeyFilter() =>
        _fixture.Provider == TestDatabaseProvider.SqlServer
            ? new SqlServerJsonObjectKeyFilter()
            : new NpgsqlJsonObjectKeyFilter();

    private static IQueryable<GridRow> BaseQuery(ReportingDbContextBase db, long formId) =>
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
        };

    private static IQueryable<GridRow> FreshOnly(IQueryable<GridRow> q) =>
        q.Where(r => r.IntegrationStatus == "processed" && r.DataJson != null && r.SourceModifiedAt == r.Stamp);

    private IQueryable<GridRow> Contains(IQueryable<GridRow> q, string key, string text) =>
        KeyFilter().WhereKeyMatches(q, nameof(GridRow.DataJson), key, text);

    // Date bounds compare the extracted text; the plan's WhereKeyAtLeast / WhereKeyBefore would do the same.
    private IQueryable<GridRow> DateBetween(IQueryable<GridRow> q, string key, string fromInclusive, string toExclusive)
    {
        var p = Expression.Parameter(typeof(GridRow), "e");
        Expression extract = _fixture.Provider == TestDatabaseProvider.SqlServer
            ? Expression.Call(Environment.GetEnvironmentVariable("SPIKE_JSON_MAX") == "1" ? SqlServerJsonDbFunctions.JsonValueMaxMethod : SqlServerJsonDbFunctions.JsonValueMethod, Expression.Property(p, nameof(GridRow.DataJson)),
                Expression.Constant(SqlServerJsonObjectKeyFilter.BuildJsonValuePath(key)))
            : Expression.Call(NpgsqlJsonDbFunctions.ExtractObjectKeyTextMethod, Expression.Property(p, nameof(GridRow.DataJson)),
                Expression.Constant(key));
        var compare = typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!;
        var body = Expression.AndAlso(
            Expression.GreaterThanOrEqual(Expression.Call(compare, extract, Expression.Constant(fromInclusive)), Expression.Constant(0)),
            Expression.LessThan(Expression.Call(compare, extract, Expression.Constant(toExclusive)), Expression.Constant(0)));
        return q.Where(Expression.Lambda<Func<GridRow, bool>>(body, p));
    }

    // ---------------------------------------------------------------- risk 1: cross-context query

    [Fact]
    public async Task Risk1_MappingStaysOutOfMigrations()
    {
        var ct = TestContext.Current.CancellationToken;
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, ct);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, ct);
        await using var db = ReportingTestSchema.CreateContext(_fixture.ConnectionString, _fixture.Provider, new IntegrationTenantContext(TenantId));

        // 1. EF sees no drift between the committed snapshot and the model that now holds CoreSubmissionRow.
        db.Database.HasPendingModelChanges().Should().BeFalse("the keyless row is excluded from migrations");

        // 2. The relational model marks the table as excluded, and the create script does not mention it.
        var table = db.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables
            .Single(t => t.Name == "Submissions");
        table.IsExcludedFromMigrations.Should().BeTrue();
        table.Schema.Should().Be(_fixture.Provider == TestDatabaseProvider.SqlServer ? "dbo" : "public");
        var script = db.Database.GenerateCreateScript();
        System.Text.RegularExpressions.Regex.IsMatch(script, @"CREATE TABLE\s+(\S+\.)?\W?Submissions\W").Should().BeFalse("no CREATE TABLE for the core table");

        // 3. The applied migrations created no such table in the reporting schema.
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync(ct);
        var count = await ScalarAsync<long>(conn, _fixture.Provider == TestDatabaseProvider.SqlServer
            ? "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='reporting' AND TABLE_NAME='Submissions'"
            : "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='reporting' AND table_name='Submissions'", ct);
        count.Should().Be(0);
        Log("risk1.migrations", $"provider={_fixture.Provider} pendingChanges=false excluded=true schema='{table.Schema}' reportingSubmissionsTables=0");
    }

    [Fact]
    public async Task Risk1_OneQueryJoinsFiltersCountsAndPages()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await SeedSmallWorldAsync(ct);

        await using var db = ReportingTestSchema.CreateContext(_fixture.ConnectionString, _fixture.Provider, new IntegrationTenantContext(TenantId));

        // No filter: left join keeps rows that were never flattened; tenant filter and soft delete apply.
        var all = BaseQuery(db, world.FormId);
        (await all.CountAsync(ct)).Should().Be(world.Visible.Count, "other tenant's and deleted rows are excluded, unflattened rows stay");

        // Contains is case-insensitive on both providers ("ACME" stored as "Acme Corp").
        var acme = FreshOnly(Contains(all, "company", "ACME"));
        var ids = await acme.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Select(r => r.Id).ToListAsync(ct);
        ids.Should().Equal(world.AcmeIdsNewestFirst);

        // Count + page in the same shape as the endpoint.
        var total = await acme.CountAsync(ct);
        var page = await acme.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Skip(1).Take(1).ToListAsync(ct);
        total.Should().Be(world.AcmeIdsNewestFirst.Count);
        page.Single().Id.Should().Be(world.AcmeIdsNewestFirst[1]);

        // Two filters AND-ed, plus a date range on a string value.
        var both = DateBetween(Contains(all, "company", "acme"), "joined", "2026-02-01", "2026-03-01");
        (await FreshOnly(both).CountAsync(ct)).Should().Be(world.AcmeInFebruary);

        // Stale row (SourceModifiedAt != Stamp) is excluded by the fresh predicate.
        (await FreshOnly(Contains(all, "company", "stale")).CountAsync(ct)).Should().Be(0);
        (await Contains(all, "company", "stale").CountAsync(ct)).Should().Be(1);

        // Wildcards in user text are escaped.
        (await Contains(all, "company", "100%").CountAsync(ct)).Should().Be(1);
        (await Contains(all, "company", "a_me").CountAsync(ct)).Should().Be(0);

        // Another tenant sees none of it.
        await using var other = ReportingTestSchema.CreateContext(_fixture.ConnectionString, _fixture.Provider, new IntegrationTenantContext(OtherTenantId));
        var otherIds = await BaseQuery(other, world.FormId).Select(r => r.Id).ToListAsync(ct);
        otherIds.Should().HaveCount(1, "only the other tenant's own row");
        otherIds.Should().NotIntersectWith(world.Visible);

        Log("risk1.sql." + _fixture.Provider, "COUNT:\n" + Contains(all, "company", "acme").OrderBy(r => r.Id).ToQueryString()
            + "\n\nPAGE:\n" + DateBetween(Contains(all, "company", "acme"), "joined", "2026-02-01", "2026-03-01").OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Skip(0).Take(50).ToQueryString());
    }

    private sealed record SmallWorld(long FormId, List<long> Visible, List<long> AcmeIdsNewestFirst, int AcmeInFebruary);

    private async Task<SmallWorld> SeedSmallWorldAsync(CancellationToken ct)
    {
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, ct);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, ct);
        await using var app = CreateAppDb(TenantId);
        await SeedTenantAsync(app, TenantId, ct);
        await SeedTenantAsync(app, OtherTenantId, ct);
        var (formId, defId) = await SeedFormAsync(app, TenantId, ct);

        var baseTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var specs = new (string Company, string Joined, bool Complete, bool Flatten, bool Stale)[]
        {
            ("Acme Corp", "2026-01-15", true, true, false),
            ("ACME Ltd", "2026-02-10", true, true, false),
            ("Globex", "2026-02-20", true, true, false),
            ("Acme in progress", "2026-02-25", false, true, false),   // in-progress, generated
            ("Unflattened Inc", "2026-02-26", false, false, false),    // no FlattenedSubmission row at all
            ("Stale Co 100% sure", "2026-02-27", true, true, true),    // flattened for an older stamp
        };

        var visible = new List<long>();
        var acme = new List<(long Id, DateTime At, string Joined)>();
        await using var reporting = ReportingTestSchema.CreateContext(_fixture.ConnectionString, _fixture.Provider, IntegrationTenantContext.Bypass);
        FlattenedRowSeed seed = new(reporting);
        for (var i = 0; i < specs.Length; i++)
        {
            var spec = specs[i];
            var at = baseTime.AddMinutes(i);
            var id = await SeedSubmissionAsync(app, TenantId, formId, defId, spec.Complete, at, ct);
            visible.Add(id);
            if (spec.Flatten)
            {
                await seed.ProcessedAsync(new FlattenedSubmissionKey(TenantId, formId, id),
                    $$"""{"company":"{{spec.Company}}","joined":"{{spec.Joined}}"}""", ct);
                var sourceStamp = spec.Stale ? at.AddDays(-1) : at;
                await reporting.FlattenedSubmissions.Where(r => r.SubmissionId == id)
                    .ExecuteUpdateAsync(u => u.SetProperty(r => r.SourceModifiedAt, sourceStamp), ct);
            }

            if (spec.Company.StartsWith("Acme", StringComparison.OrdinalIgnoreCase) && spec.Complete && !spec.Stale)
            {
                acme.Add((id, at, spec.Joined));
            }
            else if (spec.Company.Contains("acme", StringComparison.OrdinalIgnoreCase) && spec.Flatten && !spec.Stale)
            {
                acme.Add((id, at, spec.Joined));
            }
        }

        // Another tenant's submission on the SAME form id (cross-tenant leak check) and a soft-deleted one.
        await using var otherApp = CreateAppDb(OtherTenantId);
        var otherId = await SeedSubmissionAsync(otherApp, OtherTenantId, formId, defId, true, baseTime.AddMinutes(30), ct, allowForeignForm: true);
        var deletedId = await SeedSubmissionAsync(app, TenantId, formId, defId, true, baseTime.AddMinutes(31), ct);
        await app.Submissions.Where(s => s.Id == deletedId).ExecuteUpdateAsync(u => u.SetProperty(s => s.IsDeleted, true), ct);
        await seed.ProcessedAsync(new FlattenedSubmissionKey(TenantId, formId, deletedId), """{"company":"Acme deleted","joined":"2026-02-11"}""", ct);
        await seed.ProcessedAsync(new FlattenedSubmissionKey(OtherTenantId, formId, otherId), """{"company":"Acme other tenant","joined":"2026-02-11"}""", ct);

        var acmeSorted = acme.OrderByDescending(a => a.At).ThenBy(a => a.Id).ToList();
        return new SmallWorld(formId, visible, acmeSorted.Select(a => a.Id).ToList(),
            acme.Count(a => string.CompareOrdinal(a.Joined, "2026-02-01") >= 0 && string.CompareOrdinal(a.Joined, "2026-03-01") < 0));
    }

    // ---------------------------------------------------------------- risk 3b: autosave and ModifiedAt

    [Fact]
    public async Task Risk3_AutosaveOfInProgressSubmission_UpdatesModifiedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, ct);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, ct);
        await using var app = CreateAppDb(TenantId);
        await SeedTenantAsync(app, TenantId, ct);
        var (formId, defId) = await SeedFormAsync(app, TenantId, ct);
        var id = await SeedSubmissionAsync(app, TenantId, formId, defId, isComplete: false, DateTime.UtcNow.AddMinutes(-10), ct);

        await using var first = CreateAppDb(TenantId);
        var before = await first.Submissions.AsNoTracking().Where(s => s.Id == id).Select(s => s.ModifiedAt).SingleAsync(ct);

        // The same path UpdateSubmissionHandler takes: load, Update(isComplete: false), SaveChanges.
        await using var save = CreateAppDb(TenantId);
        var sub = await save.Submissions.SingleAsync(s => s.Id == id, ct);
        sub.Update("""{"answer":"autosaved"}""", defId, formId, isComplete: false, currentPage: 2);
        await save.SaveChangesAsync(ct);

        await using var read = CreateAppDb(TenantId);
        var row = await read.Submissions.AsNoTracking().Where(s => s.Id == id).Select(s => new { s.ModifiedAt, s.IsComplete }).SingleAsync(ct);
        row.IsComplete.Should().BeFalse();
        row.ModifiedAt.Should().NotBeNull();

        // And an autosave that changes nothing leaves ModifiedAt alone (so no false staleness).
        await using var save2 = CreateAppDb(TenantId);
        var sub2 = await save2.Submissions.SingleAsync(s => s.Id == id, ct);
        var stamp = sub2.ModifiedAt;
        sub2.Update("""{"answer":"autosaved"}""", defId, formId, isComplete: false, currentPage: 2);
        await save2.SaveChangesAsync(ct);
        await using var read2 = CreateAppDb(TenantId);
        var after2 = await read2.Submissions.AsNoTracking().Where(s => s.Id == id).Select(s => s.ModifiedAt).SingleAsync(ct);

        Log("risk3.autosave", $"provider={_fixture.Provider} modifiedAtBefore={before?.ToString("O") ?? "null"} afterFirstAutosave={row.ModifiedAt:O} afterNoOpAutosave={after2:O} noOpUnchanged={after2 == stamp}");
    }

    // ---------------------------------------------------------------- risk 2: SQL Server JSON limits

    [Fact]
    [Trait("DbSpecific", "SqlServer")]
    public async Task Risk2_SqlServerJsonValueLimitsAndCollation()
    {
        var ct = TestContext.Current.CancellationToken;
        if (_fixture.Provider != TestDatabaseProvider.SqlServer)
        {
            return;
        }

        await using var app = CreateAppDb(TenantId);
        var conn = app.Database.GetDbConnection();
        await conn.OpenAsync(ct);
        var sb = new StringBuilder();

        await ExecAsync(conn, "DROP TABLE IF EXISTS dbo.SpikeJson; CREATE TABLE dbo.SpikeJson (Id int NOT NULL PRIMARY KEY, J json NOT NULL);", ct);
        foreach (var len in new[] { 10, 4000, 4001, 8000, 50_000, 1_000_000 })
        {
            var text = new string('x', len - 4) + "ABCD"; // ends with a unique tail so a match can be tested
            await ExecAsync(conn, "INSERT INTO dbo.SpikeJson VALUES (@id, CAST(@j AS json))", ct,
                ("@id", len), ("@j", "{\"k\":\"" + text + "\"}"));
        }

        sb.AppendLine("len | JSON_VALUE len | JSON_VALUE ... RETURNING nvarchar(max) len | OPENJSON WITH nvarchar(max) len | LIKE '%ABCD' via JSON_VALUE | via RETURNING | via OPENJSON");
        var rows = await QueryAsync(conn, """
            SELECT t.Id,
                   LEN(JSON_VALUE(t.J, '$.k')),
                   LEN(JSON_VALUE(t.J, '$.k' RETURNING nvarchar(max))),
                   (SELECT LEN(o.k) FROM OPENJSON(t.J) WITH (k nvarchar(max) '$.k') o),
                   CASE WHEN JSON_VALUE(t.J, '$.k') LIKE N'%abcd' THEN 1 ELSE 0 END,
                   CASE WHEN JSON_VALUE(t.J, '$.k' RETURNING nvarchar(max)) LIKE N'%abcd' THEN 1 ELSE 0 END,
                   (SELECT CASE WHEN o.k LIKE N'%abcd' THEN 1 ELSE 0 END FROM OPENJSON(t.J) WITH (k nvarchar(max) '$.k') o)
            FROM dbo.SpikeJson t ORDER BY t.Id
            """, ct);
        foreach (var r in rows) { sb.AppendLine(string.Join(" | ", r.Select(v => v is null or DBNull ? "NULL" : v.ToString()))); }

        // Strict mode and error behaviour for the over-limit value.
        try
        {
            var strict = await ScalarAsync<object>(conn, "SELECT LEN(JSON_VALUE(J, 'strict $.k')) FROM dbo.SpikeJson WHERE Id = 8000", ct);
            sb.AppendLine("strict JSON_VALUE on 8000 chars => " + strict);
        }
        catch (Exception ex) { sb.AppendLine("strict JSON_VALUE on 8000 chars => ERROR " + ex.Message.Split('\n')[0]); }

        // The same through the real EF stack: what WhereKeyMatches does today on a long value.
        sb.AppendLine("collation: " + await ScalarAsync<string>(conn, "SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS nvarchar(100))", ct));
        sb.AppendLine("version: " + (await ScalarAsync<string>(conn, "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(50)) + ' compat=' + CAST((SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()) AS nvarchar(10))", ct)));

        // Case and accent sensitivity under the default collation; unicode round trip.
        await ExecAsync(conn, "DROP TABLE IF EXISTS dbo.SpikeJson2; CREATE TABLE dbo.SpikeJson2 (Id int NOT NULL PRIMARY KEY, J json NOT NULL);", ct);
        await ExecAsync(conn, "INSERT INTO dbo.SpikeJson2 VALUES (1, CAST(@j AS json)), (2, CAST(@k AS json)), (3, CAST(@m AS json))", ct,
            ("@j", "{\"k\":\"Acme Corp\"}"), ("@k", "{\"k\":\"Фирма Ойл ЕООД\"}"), ("@m", "{\"k\":\"Café Zoë\"}"));
        sb.AppendLine("LIKE '%ACME%'            matches Acme Corp: " + await ScalarAsync<int>(conn, "SELECT COUNT(*) FROM dbo.SpikeJson2 WHERE JSON_VALUE(J,'$.k') LIKE N'%ACME%'", ct));
        sb.AppendLine("LIKE '%фирма%' (Cyrillic): " + await ScalarAsync<int>(conn, "SELECT COUNT(*) FROM dbo.SpikeJson2 WHERE JSON_VALUE(J,'$.k') LIKE N'%фирма%'", ct));
        sb.AppendLine("LIKE '%cafe%' (accent):    " + await ScalarAsync<int>(conn, "SELECT COUNT(*) FROM dbo.SpikeJson2 WHERE JSON_VALUE(J,'$.k') LIKE N'%cafe%'", ct));
        sb.AppendLine("LIKE '%café%':             " + await ScalarAsync<int>(conn, "SELECT COUNT(*) FROM dbo.SpikeJson2 WHERE JSON_VALUE(J,'$.k') LIKE N'%café%'", ct));
        sb.AppendLine("LIKE '%ACME%' COLLATE Latin1_General_100_CS_AS: " + await ScalarAsync<int>(conn, "SELECT COUNT(*) FROM dbo.SpikeJson2 WHERE JSON_VALUE(J,'$.k') COLLATE Latin1_General_100_CS_AS LIKE N'%ACME%'", ct));
        sb.AppendLine("LIKE '%cafe%' COLLATE Latin1_General_100_CI_AI: " + await ScalarAsync<int>(conn, "SELECT COUNT(*) FROM dbo.SpikeJson2 WHERE JSON_VALUE(J,'$.k') COLLATE Latin1_General_100_CI_AI LIKE N'%cafe%'", ct));
        sb.AppendLine("LIKE '%ACME%' under a CS database collation (simulated via COLLATE SQL_Latin1_General_CP1_CS_AS): " + await ScalarAsync<int>(conn, "SELECT COUNT(*) FROM dbo.SpikeJson2 WHERE JSON_VALUE(J,'$.k') LIKE N'%ACME%' COLLATE SQL_Latin1_General_CP1_CS_AS", ct));

        await ExecAsync(conn, "DROP TABLE dbo.SpikeJson; DROP TABLE dbo.SpikeJson2;", ct);
        Log("risk2.sqlserver", sb.ToString());
    }

    [Fact]
    [Trait("DbSpecific", "SqlServer")]
    public async Task Risk2_SqlServerLongValueThroughEfFilter()
    {
        var ct = TestContext.Current.CancellationToken;
        if (_fixture.Provider != TestDatabaseProvider.SqlServer) { return; }
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, ct);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, ct);
        await using var app = CreateAppDb(TenantId);
        await SeedTenantAsync(app, TenantId, ct);
        var (formId, defId) = await SeedFormAsync(app, TenantId, ct);
        var id = await SeedSubmissionAsync(app, TenantId, formId, defId, true, DateTime.UtcNow.AddMinutes(-5), ct);
        await using var db = ReportingTestSchema.CreateContext(_fixture.ConnectionString, _fixture.Provider, new IntegrationTenantContext(TenantId));
        await new FlattenedRowSeed(db).ProcessedAsync(new FlattenedSubmissionKey(TenantId, formId, id),
            "{\"comment\":\"" + new string('x', 6000) + " NeedleAtTheEnd\"}", ct);
        var viaDefault = await Contains(BaseQuery(db, formId), "comment", "needleattheend").CountAsync(ct);
        var sb = new StringBuilder($"matches long (6000+ chars) value with the current JSON_VALUE filter: {viaDefault}\n");
        sb.AppendLine("env SPIKE_JSON_MAX=" + Environment.GetEnvironmentVariable("SPIKE_JSON_MAX"));
        sb.AppendLine(Contains(BaseQuery(db, formId), "comment", "needleattheend").ToQueryString());
        Log("risk2.ef.sqlserver.max" + Environment.GetEnvironmentVariable("SPIKE_JSON_MAX"), sb.ToString());
    }

    // ---------------------------------------------------------------- risk 3: performance

    private static readonly string[] FilterKeys = ["q0010", "q0500", "q0990"];

    [Fact]
    public async Task Risk3_Performance()
    {
        var ct = TestContext.Current.CancellationToken;
        if (Environment.GetEnvironmentVariable("SPIKE_PERF") != "1")
        {
            return;
        }

        var rows = int.Parse(Environment.GetEnvironmentVariable("SPIKE_ROWS") ?? "50000", CultureInfo.InvariantCulture);
        var cols = int.Parse(Environment.GetEnvironmentVariable("SPIKE_COLS") ?? "1000", CultureInfo.InvariantCulture);
        var iterations = int.Parse(Environment.GetEnvironmentVariable("SPIKE_ITER") ?? "20", CultureInfo.InvariantCulture);
        var label = $"{_fixture.Provider} rows={rows} cols={cols}";

        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, ct);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, ct);
        await using var app = CreateAppDb(TenantId);
        await SeedTenantAsync(app, TenantId, ct);
        var (formId, defId) = await SeedFormAsync(app, TenantId, ct);
        var templateId = await SeedSubmissionAsync(app, TenantId, formId, defId, true, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), ct);

        var sw = Stopwatch.StartNew();
        var expected = await LoadDatasetAsync(app, formId, templateId, rows, cols, ct);
        Log("perf.load." + label, $"loaded in {sw.Elapsed.TotalSeconds:F0}s; expected counts: {expected}");
        await AnalyzeAsync(app, ct);
        await DescribeStorageAsync(app, label, ct);

        var first = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var scenarios = new (string Name, Func<ReportingDbContextBase, IQueryable<GridRow>> Build, int Expected)[]
        {
            ("no answer filter (metadata only)", db => BaseQuery(db, formId), expected.Total),
            ("fresh only, no filter", db => FreshOnly(BaseQuery(db, formId)), expected.Total),
            ("1 contains (1% hit)", db => Contains(FreshOnly(BaseQuery(db, formId)), "q0010", "ZEBRA"), expected.Zebra),
            ("1 contains (25% hit)", db => Contains(FreshOnly(BaseQuery(db, formId)), "q0990", "mango"), expected.Mango),
            ("3 contains (AND, 1% hit)", db => Contains(Contains(Contains(FreshOnly(BaseQuery(db, formId)), "q0010", "zebra"), "q0500", "kiwi"), "q0990", "mango"), expected.All3),
            ("3 contains, none selective first (25%,5%,1%)", db => Contains(Contains(Contains(FreshOnly(BaseQuery(db, formId)), "q0990", "mango"), "q0500", "kiwi"), "q0010", "zebra"), expected.All3),
            ("date range only (1 month)", db => DateBetween(FreshOnly(BaseQuery(db, formId)), "q0007", "2025-06-01", "2025-07-01"), expected.Dates),
            ("3 contains + date range", db => DateBetween(Contains(Contains(Contains(FreshOnly(BaseQuery(db, formId)), "q0010", "zebra"), "q0500", "kiwi"), "q0990", "mango"), "q0007", "2025-01-01", "2026-01-01"), expected.All3InYear),
            ("contains that matches nothing (full scan)", db => Contains(FreshOnly(BaseQuery(db, formId)), "q0500", "qqqqqq"), 0),
        };

        var report = new StringBuilder();
        report.AppendLine($"## {label} iterations={iterations}");
        report.AppendLine("| scenario | total | count p50 | count p95 | page1 p50 | page1 p95 | count+page1 p95 | page100 p95 |");
        report.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var (name, build, expectedCount) in scenarios)
        {
            var counts = new List<double>();
            var pages = new List<double>();
            var deep = new List<double>();
            var both = new List<double>();
            int total = -1;
            for (var i = -3; i < iterations; i++)
            {
                await using var c1 = ReportingTestSchema.CreateContext(_fixture.ConnectionString, _fixture.Provider, new IntegrationTenantContext(TenantId));
                var t0 = Stopwatch.GetTimestamp();
                total = await build(c1).AsNoTracking().CountAsync(ct);
                var t1 = Stopwatch.GetTimestamp();
                var page = await build(c1).AsNoTracking().OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Skip(0).Take(50).ToListAsync(ct);
                var t2 = Stopwatch.GetTimestamp();
                var skip = Math.Min(4950, Math.Max(0, total - 50));
                var page100 = await build(c1).AsNoTracking().OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Skip(skip).Take(50).ToListAsync(ct);
                var t3 = Stopwatch.GetTimestamp();
                if (i >= 0)
                {
                    counts.Add(Stopwatch.GetElapsedTime(t0, t1).TotalMilliseconds);
                    pages.Add(Stopwatch.GetElapsedTime(t1, t2).TotalMilliseconds);
                    both.Add(Stopwatch.GetElapsedTime(t0, t2).TotalMilliseconds);
                    deep.Add(Stopwatch.GetElapsedTime(t2, t3).TotalMilliseconds);
                }

                page.Count.Should().Be(Math.Min(50, total));
            }

            total.Should().Be(expectedCount, name);
            report.AppendLine($"| {name} | {total} | {P(counts, .5):F0} | {P(counts, .95):F0} | {P(pages, .5):F0} | {P(pages, .95):F0} | {P(both, .95):F0} | {P(deep, .95):F0} |");
        }

        Log("perf.result." + label, report.ToString());

        if (_fixture.Provider == TestDatabaseProvider.PostgreSql)
        {
            await using var ex = ReportingTestSchema.CreateContext(_fixture.ConnectionString, _fixture.Provider, new IntegrationTenantContext(TenantId));
            var conn = ex.Database.GetDbConnection();
            await conn.OpenAsync(ct);
            await ExecAsync(conn, "SET track_io_timing = on", ct);
            foreach (var (name, build, _) in scenarios.Where(s => s.Name.StartsWith("1 contains (1%") || s.Name.StartsWith("3 contains (AND") || s.Name.StartsWith("fresh only") || s.Name.StartsWith("contains that")))
            {
                var countSql = InlineParameters(build(ex).AsNoTracking().GroupBy(r => 1).Select(g => g.Count()).ToQueryString());
                var pageSql = InlineParameters(build(ex).AsNoTracking().OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Skip(0).Take(50).ToQueryString());
                var plan = new StringBuilder();
                plan.AppendLine("-- COUNT query");
                foreach (var line in await QueryAsync(conn, "EXPLAIN (ANALYZE, BUFFERS, TIMING) " + countSql, ct)) { plan.AppendLine(line[0]?.ToString()); }
                plan.AppendLine("-- PAGE query");
                foreach (var line in await QueryAsync(conn, "EXPLAIN (ANALYZE, BUFFERS, TIMING) " + pageSql, ct)) { plan.AppendLine(line[0]?.ToString()); }
                Log("perf.explain.pg." + name, plan.ToString());
            }
        }
    }

    [Fact]
    public async Task Risk3_PgMitigations()
    {
        var ct = TestContext.Current.CancellationToken;
        if (Environment.GetEnvironmentVariable("SPIKE_PERF") != "2" || _fixture.Provider != TestDatabaseProvider.PostgreSql) { return; }
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, ct);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, ct);
        await using var app = CreateAppDb(TenantId);
        await SeedTenantAsync(app, TenantId, ct);
        var (formId, defId) = await SeedFormAsync(app, TenantId, ct);
        var templateId = await SeedSubmissionAsync(app, TenantId, formId, defId, true, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), ct);
        await LoadDatasetAsync(app, formId, templateId, 50000, 1000, ct);
        var conn = app.Database.GetDbConnection();
        var sb = new StringBuilder();
        sb.AppendLine("server: " + await ScalarAsync<string>(conn, "SHOW server_version", ct));
        const string F = "reporting.\"FlattenedSubmissions\"";
        async Task Measure(string label, string sql)
        {
            var times = new List<double>();
            for (var i = -2; i < 7; i++)
            {
                var t = Stopwatch.GetTimestamp();
                _ = await ScalarAsync<object>(conn, sql, ct);
                if (i >= 0) times.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds);
            }
            sb.AppendLine($"{label}: p50={P(times, .5):F0}ms max={times.Max():F0}ms");
        }
        string one = $"SELECT count(*) FROM {F} WHERE jsonb_extract_path_text(\"DataJson\", 'q0010') ILIKE '%zebra%'";
        string three = $"SELECT count(*) FROM {F} WHERE jsonb_extract_path_text(\"DataJson\", 'q0990') ILIKE '%mango%' AND jsonb_extract_path_text(\"DataJson\", 'q0500') ILIKE '%kiwi%' AND jsonb_extract_path_text(\"DataJson\", 'q0010') ILIKE '%zebra%'";
        await Measure("baseline pglz (default) 1 contains", one);
        await Measure("baseline pglz (default) 3 contains (25% first)", three);

        await ExecAsync(conn, $"ALTER TABLE {F} ALTER COLUMN \"DataJson\" SET COMPRESSION lz4; UPDATE {F} SET \"DataJson\" = \"DataJson\" || '{{}}'::jsonb; VACUUM FULL ANALYZE {F};", ct);
        sb.AppendLine("size after lz4: " + await ScalarAsync<string>(conn, $"SELECT pg_size_pretty(pg_total_relation_size('{F}'))", ct));
        await Measure("lz4 1 contains", one);
        await Measure("lz4 3 contains", three);

        await ExecAsync(conn, $"ALTER TABLE {F} ALTER COLUMN \"DataJson\" SET STORAGE EXTERNAL; UPDATE {F} SET \"DataJson\" = \"DataJson\" || '{{}}'::jsonb; VACUUM FULL ANALYZE {F};", ct);
        sb.AppendLine("size after EXTERNAL (uncompressed toast): " + await ScalarAsync<string>(conn, $"SELECT pg_size_pretty(pg_total_relation_size('{F}'))", ct));
        await Measure("EXTERNAL (no compression) 1 contains", one);
        await Measure("EXTERNAL (no compression) 3 contains", three);

        // Escalation 1: narrow search column with only the filterable keys (here 100 keys, lowercased).
        await ExecAsync(conn, $"ALTER TABLE {F} ADD COLUMN \"SearchJson\" jsonb; UPDATE {F} SET \"SearchJson\" = (SELECT jsonb_object_agg(k, lower(v)) FROM (SELECT 'q' || lpad(g::text, 4, '0') AS k, jsonb_extract_path_text(\"DataJson\", 'q' || lpad(g::text, 4, '0')) AS v FROM generate_series(1, 100) g) x); UPDATE {F} SET \"SearchJson\" = \"SearchJson\" || jsonb_build_object('q0500', lower(jsonb_extract_path_text(\"DataJson\", 'q0500')), 'q0990', lower(jsonb_extract_path_text(\"DataJson\", 'q0990'))); VACUUM FULL ANALYZE {F};", ct);
        sb.AppendLine("avg SearchJson bytes: " + await ScalarAsync<object>(conn, $"SELECT avg(pg_column_size(\"SearchJson\")) FROM {F}", ct));
        await Measure("narrow SearchJson(~100 keys) 1 contains LIKE", $"SELECT count(*) FROM {F} WHERE jsonb_extract_path_text(\"SearchJson\", 'q0010') LIKE '%zebra%'");
        await Measure("narrow SearchJson 3 contains", $"SELECT count(*) FROM {F} WHERE jsonb_extract_path_text(\"SearchJson\", 'q0990') LIKE '%mango%' AND jsonb_extract_path_text(\"SearchJson\", 'q0500') LIKE '%kiwi%' AND jsonb_extract_path_text(\"SearchJson\", 'q0010') LIKE '%zebra%'");
        Log("perf.pg.mitigations", sb.ToString());
    }

    private sealed record Expected(int Total, int Zebra, int Mango, int All3, int Dates, int All3InYear);

    private async Task<Expected> LoadDatasetAsync(AppDbContext app, long formId, long templateId, int rows, int cols, CancellationToken ct)
    {
        var conn = app.Database.GetDbConnection();
        await conn.OpenAsync(ct);
        var sql = _fixture.Provider == TestDatabaseProvider.SqlServer;
        string Q(string n) => sql ? $"[{n}]" : $"\"{n}\"";
        var baseId = 5_000_000_000_000L;
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // 1. Clone the template submission rows (core table), set-based.
        var colNames = (await QueryAsync(conn, sql
            ? "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Submissions' AND TABLE_SCHEMA = 'dbo' ORDER BY ORDINAL_POSITION"
            : "SELECT column_name FROM information_schema.columns WHERE table_name = 'Submissions' AND table_schema = 'public' ORDER BY ordinal_position", ct))
            .Select(r => r[0]!.ToString()!).ToList();
        string Expr(string c) => c switch
        {
            "Id" => sql ? $"{baseId} + n.i" : $"{baseId} + n.i",
            "CreatedAt" => sql ? "DATEADD(MINUTE, n.i, CAST('2026-01-01T00:00:00' AS datetime2))" : "(timestamptz '2026-01-01 00:00:00+00' + n.i * interval '1 minute')",
            "ModifiedAt" => "NULL",
            _ => $"t.{Q(c)}",
        };
        var insertList = string.Join(", ", colNames.Select(Q));
        var selectList = string.Join(", ", colNames.Select(Expr));
        var generator = sql
            ? $"(SELECT TOP ({rows}) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS bigint) AS i FROM sys.all_objects a CROSS JOIN sys.all_objects b CROSS JOIN sys.all_objects c) n"
            : $"(SELECT generate_series(1, {rows})::bigint AS i) n";
        await ExecAsync(conn, $"INSERT INTO {Q("Submissions")} ({insertList}) SELECT {selectList} FROM {Q("Submissions")} t CROSS JOIN {generator} WHERE t.{Q("Id")} = {templateId}", ct);

        await ExecAsync(conn, $"DELETE FROM {Q("Submissions")} WHERE {Q("Id")} = {templateId}", ct);

        // 2. Flattened rows with wide JSON, in batches.
        var vocab = BuildVocabulary();
        int zebra = 0, mango = 0, all3 = 0, dates = 0, all3InYear = 0;
        const int batch = 50;
        for (var from = 1; from <= rows; from += batch)
        {
            var to = Math.Min(rows, from + batch - 1);
            var values = new StringBuilder();
            var ps = new List<(string, object)>();
            for (var i = from; i <= to; i++)
            {
                var json = BuildRowJson(i, cols, vocab, out var hasZebra, out var hasKiwi, out var hasMango, out var date);
                if (hasZebra) zebra++;
                if (hasMango) mango++;
                if (hasZebra && hasKiwi && hasMango) { all3++; if (string.CompareOrdinal(date, "2025-01-01") >= 0 && string.CompareOrdinal(date, "2026-01-01") < 0) all3InYear++; }
                if (string.CompareOrdinal(date, "2025-06-01") >= 0 && string.CompareOrdinal(date, "2025-07-01") < 0) dates++;
                var created = start.AddMinutes(i).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                var pn = "@j" + (i - from);
                ps.Add((pn, json));
                var ts = sql ? $"CAST('{created}' AS datetime2)" : $"timestamptz '{created}+00'";
                var cast = sql ? $"CAST({pn} AS json)" : $"CAST({pn} AS jsonb)";
                var bool0 = sql ? "0" : "false";
                values.Append(values.Length > 0 ? "," : "").Append($"({baseId + i},{TenantId},{formId},{cast},{bool0},{ts},NULL,{ts},1,'processed',{ts},{ts},NULL)");
            }

            await ExecAsync(conn,
                $"INSERT INTO {Q("reporting")}.{Q("FlattenedSubmissions")} ({Q("SubmissionId")},{Q("TenantId")},{Q("FormId")},{Q("DataJson")},{Q("IsDeleted")},{Q("CreatedAt")},{Q("ModifiedAt")},{Q("SourceModifiedAt")},{Q("SourceRevision")},{Q("IntegrationStatus")},{Q("Integration_LastAttemptAt")},{Q("Integration_ProcessedAt")},{Q("Integration_LastError")}) VALUES {values}",
                ct, ps.ToArray());
        }

        return new Expected(rows, zebra, mango, all3, dates, all3InYear);
    }

    private static string[] BuildVocabulary()
    {
        // Pseudo-words from syllables that cannot spell the planted terms (zebra / kiwi / mango).
        string[] syl = ["ba", "do", "li", "mu", "ne", "po", "ra", "su", "ti", "vo", "ka", "he", "lo", "pi", "te", "ru"];
        var rnd = new Random(7);
        var words = new List<string>();
        while (words.Count < 4000)
        {
            var w = string.Concat(Enumerable.Range(0, rnd.Next(2, 5)).Select(_ => syl[rnd.Next(syl.Length)]));
            if (!w.Contains("kiwi", StringComparison.Ordinal) && !w.Contains("mango", StringComparison.Ordinal) && !w.Contains("zebra", StringComparison.Ordinal))
            {
                words.Add(w);
            }
        }

        return [.. words];
    }

    private static string BuildRowJson(int i, int cols, string[] vocab, out bool zebra, out bool kiwi, out bool mango, out string date)
    {
        var sb = new StringBuilder(cols * 28);
        sb.Append('{');
        zebra = i % 100 == 0;
        kiwi = i % 20 == 0;
        mango = i % 4 == 0;
        date = new DateTime(2025, 1, 1).AddDays(i % 730).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        for (var k = 1; k <= cols; k++)
        {
            if (k > 1) sb.Append(',');
            string value;
            if (k == 7) value = date;
            else if (k == 10) value = zebra ? "A Zebra crossing " + vocab[(i * 31 + k) % vocab.Length] : vocab[(i * 31 + k) % vocab.Length] + " " + vocab[(i * 17 + k) % vocab.Length];
            else if (k == 500) value = kiwi ? "Kiwi " + vocab[(i * 31 + k) % vocab.Length] : vocab[(i * 31 + k) % vocab.Length] + " " + vocab[(i * 17 + k) % vocab.Length];
            else if (k == 990) value = mango ? "mango " + vocab[(i * 31 + k) % vocab.Length] : vocab[(i * 31 + k) % vocab.Length] + " " + vocab[(i * 17 + k) % vocab.Length];
            else value = vocab[(i * 31 + k * 17) % vocab.Length] + " " + vocab[(i * 13 + k * 7) % vocab.Length];
            sb.Append("\"q").Append(k.ToString("D4", CultureInfo.InvariantCulture)).Append("\":\"").Append(value).Append('"');
        }

        return sb.Append('}').ToString();
    }

    private async Task AnalyzeAsync(AppDbContext app, CancellationToken ct)
    {
        var conn = app.Database.GetDbConnection();
        if (_fixture.Provider == TestDatabaseProvider.PostgreSql)
        {
            await ExecAsync(conn, "VACUUM ANALYZE \"Submissions\"; VACUUM ANALYZE reporting.\"FlattenedSubmissions\";", ct);
        }
        else
        {
            await ExecAsync(conn, "UPDATE STATISTICS dbo.Submissions WITH FULLSCAN; UPDATE STATISTICS reporting.FlattenedSubmissions WITH FULLSCAN;", ct);
        }
    }

    private async Task DescribeStorageAsync(AppDbContext app, string label, CancellationToken ct)
    {
        var conn = app.Database.GetDbConnection();
        var sb = new StringBuilder();
        if (_fixture.Provider == TestDatabaseProvider.PostgreSql)
        {
            foreach (var r in await QueryAsync(conn, """
                SELECT c.relname, pg_size_pretty(pg_relation_size(c.oid)) AS heap, pg_size_pretty(pg_total_relation_size(c.oid)) AS total,
                       (SELECT pg_size_pretty(pg_relation_size(c.reltoastrelid))) AS toast
                FROM pg_class c WHERE c.relname IN ('FlattenedSubmissions','Submissions')
                """, ct)) { sb.AppendLine(string.Join(" | ", r)); }
            sb.AppendLine("avg DataJson bytes (pg_column_size, stored/compressed): " + await ScalarAsync<object>(conn, "SELECT avg(pg_column_size(\"DataJson\")) FROM reporting.\"FlattenedSubmissions\"", ct));
            sb.AppendLine("avg DataJson bytes (octet_length of text): " + await ScalarAsync<object>(conn, "SELECT avg(octet_length(\"DataJson\"::text)) FROM reporting.\"FlattenedSubmissions\"", ct));
            sb.AppendLine("shared_buffers: " + await ScalarAsync<string>(conn, "SHOW shared_buffers", ct));
        }
        else
        {
            foreach (var r in await QueryAsync(conn, """
                SELECT t.name, SUM(a.total_pages) * 8 / 1024 AS total_mb, SUM(a.used_pages) * 8 / 1024 AS used_mb
                FROM sys.tables t JOIN sys.indexes i ON i.object_id = t.object_id JOIN sys.partitions p ON p.object_id = i.object_id AND p.index_id = i.index_id
                JOIN sys.allocation_units a ON a.container_id = p.partition_id WHERE t.name IN ('FlattenedSubmissions','Submissions') GROUP BY t.name
                """, ct)) { sb.AppendLine(string.Join(" | ", r)); }
            sb.AppendLine("avg DATALENGTH(DataJson): " + await ScalarAsync<object>(conn, "SELECT AVG(CAST(DATALENGTH(DataJson) AS bigint)) FROM reporting.FlattenedSubmissions", ct));
        }

        Log("perf.storage." + label, sb.ToString());
    }

    private static double P(List<double> xs, double p)
    {
        var s = xs.OrderBy(x => x).ToList();
        return s[Math.Min(s.Count - 1, (int)Math.Ceiling(p * s.Count) - 1)];
    }

    private static string InlineParameters(string queryString)
    {
        var lines = queryString.Split('\n');
        var sql = new StringBuilder();
        var map = new Dictionary<string, string>();
        foreach (var line in lines)
        {
            var m = Regex.Match(line, @"^-- (@\w+)='(.*?)'( \(.*\))?\s*$");
            if (m.Success) { map[m.Groups[1].Value] = m.Groups[2].Value; continue; }
            if (line.StartsWith("--", StringComparison.Ordinal)) continue;
            sql.AppendLine(line);
        }

        var result = sql.ToString();
        foreach (var (k, v) in map.OrderByDescending(x => x.Key.Length)) { result = result.Replace(k, "'" + v.Replace("'", "''") + "'"); }
        return result;
    }

    // ---------------------------------------------------------------- seeding / ado helpers

    private AppDbContext CreateAppDb(long tenantId)
    {
        DbContextOptionsBuilder<AppDbContext> ob = new();
        IntegrationAppDbContextFactory.ConfigureOptions(ob, _fixture.ConnectionString, _fixture.Provider);
        return new AppDbContext(ob.Options, new IntegrationTenantContext(tenantId), new OutboxIntegrationEventDispatcher());
    }

    private static async Task SeedTenantAsync(AppDbContext app, long id, CancellationToken ct)
    {
        app.Set<Tenant>().Add(new Tenant($"spike-{id}", $"tn{id:D6}") { Id = id });
        await app.SaveChangesAsync(ct);
        app.ChangeTracker.Clear();
    }

    private static async Task<(long FormId, long DefId)> SeedFormAsync(AppDbContext app, long tenantId, CancellationToken ct)
    {
        var form = Form.Create(new FormCreateArgs(TenantId: tenantId, Name: "Spike form"));
        app.Forms.Add(form);
        await app.SaveChangesAsync(ct);
        FormDefinition definition = new(tenantId, isDraft: false, jsonData: """{"pages":[]}""");
        form.AddFormDefinition(definition);
        app.Set<FormDefinition>().Add(definition);
        await app.SaveChangesAsync(ct);
        app.ChangeTracker.Clear();
        return (form.Id, definition.Id);
    }

    private static async Task<long> SeedSubmissionAsync(AppDbContext app, long tenantId, long formId, long defId, bool isComplete, DateTime createdAt, CancellationToken ct, bool allowForeignForm = false)
    {
        _ = allowForeignForm;
        var submission = Submission.Create(new SubmissionCreateArgs(TenantId: tenantId, FormId: formId, FormDefinitionId: defId, JsonData: """{"answer":"x"}""", IsComplete: isComplete));
        app.Submissions.Add(submission);
        await app.SaveChangesAsync(ct);
        var id = submission.Id;
        app.ChangeTracker.Clear();
        await app.Submissions.IgnoreQueryFilters().Where(s => s.Id == id)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.CreatedAt, createdAt), ct);
        return id;
    }

    private static async Task ExecAsync(DbConnection conn, string sql, CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 600;
        foreach (var (name, value) in parameters)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<T> ScalarAsync<T>(DbConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var v = await cmd.ExecuteScalarAsync(ct);
        return (T)Convert.ChangeType(v!, typeof(T) == typeof(object) ? v!.GetType() : typeof(T), CultureInfo.InvariantCulture);
    }

    private static async Task<List<object?[]>> QueryAsync(DbConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 300;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<object?[]>();
        while (await reader.ReadAsync(ct))
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++) { row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i); }
            rows.Add(row);
        }

        return rows;
    }

    private static void Log(string name, string text)
    {
        var dir = Environment.GetEnvironmentVariable("SPIKE_OUT") ?? Path.GetTempPath();
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name.Replace(' ', '_').Replace('/', '_').Replace('(', '_').Replace(')', '_').Replace('%', 'p') + ".txt"), text);
    }
}
