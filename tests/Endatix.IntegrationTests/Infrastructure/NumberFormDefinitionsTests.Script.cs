using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.IntegrationTests;

public sealed partial class NumberFormDefinitionsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scripted_upgrade_preserves_the_edit_whether_the_old_writer_commits_before_or_after_finishing(bool commitAfter)
    {
        await using var host = await OwnDatabaseWebHost.StartAsync(autoMigrate: false);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, Ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: Ct);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        scope.ServiceProvider.GetRequiredService<IOptions<DataOptions>>().Value.EnableAutoMigrations.Should().BeFalse();
        var other = await CreateUnaffectedFormAsync(admin);
        var form = await CreateFormAsync(admin);
        var active = (await db.Forms.SingleAsync(row => row.Id == form, Ct)).ActiveDefinitionId!.Value;
        await DeploySqlAsync(db, active);
        await AssertPendingAsync(admin, form);
        using var unaffected = await admin.GetAsync($"/api/forms/{other}/draft", Ct);
        unaffected.EnsureSuccessStatusCode();

        // A by-ID writer materializes a visible row in one statement, then saves by key.
        // The previous release knows neither the queue nor the publish provenance column.
        await using var oldModel = new LegacyDefinitionContext(host.Database);
        var oldRead = await oldModel.Set<LegacyDefinition>().SingleAsync(row => row.FormId == form && row.Id == 103, Ct);
        var revision = await db.Forms.Where(row => row.Id == form).Select(row => row.Revision).SingleAsync(Ct);
        if (!commitAfter)
        {
            await SaveOldModelAsync(oldModel, oldRead);
        }
        await AssertInterruptedAsync(host.Services, admin, form);
        if (commitAfter)
        {
            await FinishWithWaitingWriterAsync(host.Services, oldModel, oldRead);
        }
        else
        {
            await FinishAsync(host.Services);
        }
        await FinishAsync(host.Services);
        await AssertFinishedAsync(admin, form, commitAfter);
        await AssertCompletedRowsAsync(db, form);
        await AssertCompileEventAsync(db, form, revision + 1);
        (await db.Forms.AsNoTracking().SingleAsync(row => row.Id == form, Ct)).ActiveDefinitionId.Should().Be(active);
    }

    private static async Task<long> CreateUnaffectedFormAsync(HttpClient admin)
    {
        var other = await CreateFormAsync(admin);
        using var ready = await admin.PatchAsJsonAsync($"/api/forms/{other}/definition", new { isDraft = false }, Ct);
        ready.EnsureSuccessStatusCode();
        return other;
    }

    private static async Task DeploySqlAsync(AppDbContext db, long active)
    {
        var ids = db.Database.GetMigrations().ToList();
        var previous = ids[ids.FindIndex(id => id.EndsWith("_AddFormDraftHistory", StringComparison.Ordinal)) - 1];
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(previous, Ct);
        await SeedLegacyAsync(db, active);
        var script = migrator.GenerateScript(previous, ids.Single(id => id.EndsWith("_NumberFormDefinitions", StringComparison.Ordinal)));
        await db.Database.OpenConnectionAsync(Ct);
        foreach (var batch in BatchSeparator().Split(script).Where(batch => !string.IsNullOrWhiteSpace(batch)))
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = batch;
            await command.ExecuteNonQueryAsync(Ct);
        }
        await db.Database.CloseConnectionAsync();
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex BatchSeparator();

    [GeneratedRegex(@"DELETE[\s\S]*LegacyDraftUpgrades")]
    private static partial Regex QueueRemoval();

    private static async Task AssertPendingAsync(HttpClient admin, long form)
    {
        var route = $"/api/forms/{form}/draft";
        admin.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");
        using var get = await admin.GetAsync(route, Ct);
        using var put = await admin.PutAsJsonAsync(route, new { jsonData = "{}" }, Ct);
        using var patch = await admin.PatchAsJsonAsync(route, new
        {
            saveNo = 1,
            clientSessionId = "upgrade",
            jsonData = "{}",
            records = new[] { new { seq = 1 } },
            contentHash = new string('a', 64)
        }, Ct);
        using var discard = await admin.DeleteAsync(route, Ct);
        admin.DefaultRequestHeaders.IfMatch.Clear();
        foreach (var response in new[] { get, put, patch, discard })
        {
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("detail").GetString().Should().Contain("upgrade");
        }
    }

    private static async Task SaveOldModelAsync(LegacyDefinitionContext db, LegacyDefinition row)
    {
        row.JsonData = """{"acknowledged":true}""";
        row.ModifiedAt = DateTime.UtcNow;
        (await db.SaveChangesAsync(Ct)).Should().Be(1);
    }

    private sealed class LegacyDefinition
    {
        public long Id { get; init; }
        public long FormId { get; init; }
        public bool IsDeleted { get; init; }
        public string JsonData { get; set; } = null!;
        public DateTime? ModifiedAt { get; set; }
    }

    private sealed class LegacyDefinitionContext(DatabaseInfrastructureFixture database) : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder builder)
        {
            if (database.Provider == TestDatabaseProvider.SqlServer)
            {
                builder.UseSqlServer(database.ConnectionString);
            }
            else
            {
                builder.UseNpgsql(database.ConnectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            var entity = builder.Entity<LegacyDefinition>();
            entity.ToTable("FormDefinitions").HasQueryFilter(row => !row.IsDeleted);
            entity.Property(row => row.JsonData).HasColumnType(Database.IsSqlServer() ? "json" : "jsonb");
        }
    }

    private static async Task AssertInterruptedAsync(IServiceProvider services, HttpClient admin, long form)
    {
        var gate = services.GetRequiredService<AppCommandGate>();
        using (gate.FailAt(QueueRemoval(), new InvalidOperationException("Interrupted upgrade")))
        {
            var finish = () => FinishAsync(services);
            await finish.Should().ThrowAsync<InvalidOperationException>().WithMessage("Interrupted upgrade");
        }
        await AssertPendingAsync(admin, form);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.FormDraftOperations.AnyAsync(row => row.FormId == form, Ct)).Should().BeFalse();
    }

    private static async Task FinishWithWaitingWriterAsync(IServiceProvider services, LegacyDefinitionContext oldModel, LegacyDefinition row)
    {
        await oldModel.Database.OpenConnectionAsync(Ct);
        var pidSql = oldModel.Database.IsSqlServer() ? "SELECT CAST(@@SPID AS int) AS Value" : """SELECT pg_backend_pid() AS "Value" """;
        var pid = await oldModel.Database.SqlQueryRaw<int>(pidSql).SingleAsync(Ct);
        using var paused = services.GetRequiredService<AppCommandGate>().PauseAt(QueueRemoval());
        var finishing = FinishAsync(services);
        await paused.Reached.WaitAsync(TimeSpan.FromSeconds(20), Ct);
        var writing = SaveOldModelAsync(oldModel, row);
        try
        {
            await WaitForDatabaseBlockAsync(services, pid);
        }
        finally
        {
            paused.Release();
            await Task.WhenAll(finishing, writing);
        }
    }

    private static async Task WaitForDatabaseBlockAsync(IServiceProvider services, int pid)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var query = db.Database.IsSqlServer()
            ? (FormattableString)$"SELECT COUNT(*) AS Value FROM sys.dm_exec_requests WHERE session_id={pid} AND blocking_session_id>0"
            : $"""SELECT cardinality(pg_blocking_pids({pid})) AS "Value" """;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (await db.Database.SqlQuery<int>(query).SingleAsync(timeout.Token) == 0)
        {
            await Task.Delay(25, timeout.Token);
        }
    }

    private static async Task FinishAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(Ct);
    }

    private static async Task AssertFinishedAsync(HttpClient admin, long form, bool commitAfter)
    {
        var head = await admin.GetFromJsonAsync<JsonElement>($"/api/forms/{form}/draft", Ct);
        using var adopted = JsonDocument.Parse(head.GetProperty("jsonData").GetString()!);
        using var expected = JsonDocument.Parse(commitAfter ? """{"a":1,"z":2}""" : """{"acknowledged":true}""");
        JsonElement.DeepEquals(adopted.RootElement, expected.RootElement).Should().BeTrue();
        var row = await admin.GetFromJsonAsync<JsonElement>($"/api/forms/{form}/definitions/103", Ct);
        using var current = JsonDocument.Parse(row.GetProperty("jsonData").GetString()!);
        current.RootElement.GetProperty("acknowledged").GetBoolean().Should().BeTrue();
    }

    private static async Task AssertCompletedRowsAsync(AppDbContext db, long form)
    {
        db.ChangeTracker.Clear();
        (await db.FormDraftOperations.Where(item => item.FormId == form).OrderBy(item => item.Version)
            .Select(item => item.Reason).ToListAsync(Ct)).Should().Equal("seed", "adopt");
        (await db.Submissions.Where(item => item.FormId == form).Select(item => item.FormDefinitionId).SingleAsync(Ct)).Should().Be(101);
        (await db.Set<LegacyDraftUpgrade>().AnyAsync(Ct)).Should().BeFalse();
    }
}
