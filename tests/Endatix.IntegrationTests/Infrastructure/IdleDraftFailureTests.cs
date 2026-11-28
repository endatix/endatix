using System.Text.RegularExpressions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.IntegrationTests;

[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
public sealed partial class IdleDraftFailureTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_RejectedHead_RollsBackAndContinuesWithTheNextTenant()
    {
        // Arrange
        await using var host = await OwnDatabaseWebHost.StartAsync();
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var first = await SeedTailAsync(host, services);
        var second = await SeedTailAsync(host, services);
        first.FormId.Should().BeLessThan(second.FormId);
        first.TenantId.Should().NotBe(second.TenantId);
        var reject = FormattableString.Invariant($"""
            ALTER TABLE "FormDrafts" ADD CONSTRAINT "CK_RejectIdleTest" CHECK ("FormId" <> {first.FormId} OR "Version" < 3)
            """);
        await db.Database.ExecuteSqlRawAsync(reject, Ct);
        using var logs = new CapturingLoggerProvider();
        services.GetRequiredService<ILoggerFactory>().AddProvider(logs);
        var job = services.GetRequiredKeyedService<IRecurringJob>("IdleDraftCheckpoint");

        // Act
        await job.RunAsync(Ct);

        // Assert
        await AssertVersionAsync(db, first, 2);
        await AssertVersionAsync(db, second, 3);
        var (level, message) = logs.Entries.Should().ContainSingle(entry => entry.Message.StartsWith("Idle checkpoint rejected")).Which;
        level.Should().Be(LogLevel.Error);
        message.Should().Contain($"tenant {first.TenantId}, form {first.FormId}, version 2")
            .And.NotContain("private draft").And.NotContain("CK_RejectIdleTest");
        await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "FormDrafts" DROP CONSTRAINT "CK_RejectIdleTest" """, Ct);
        await job.RunAsync(Ct);
        await job.RunAsync(Ct);
        await AssertVersionAsync(db, first, 3);
        await AssertVersionAsync(db, second, 3);
    }

    [Fact]
    public async Task RunAsync_StoreTimesOut_PropagatesTheFailureWithoutTryingTheNextWrite()
    {
        // Arrange
        await using var host = await OwnDatabaseWebHost.StartAsync();
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var first = await SeedTailAsync(host, services);
        var second = await SeedTailAsync(host, services);
        var timeout = new TimeoutException("The store did not answer.");
        using var fault = services.GetRequiredService<AppCommandGate>().FailAt(HeadUpdate(), timeout);
        var job = services.GetRequiredKeyedService<IRecurringJob>("IdleDraftCheckpoint");

        // Act
        var exception = await Record.ExceptionAsync(() => job.RunAsync(Ct));

        // Assert
        exception!.GetBaseException().Should().BeSameAs(timeout);
        var db = services.GetRequiredService<AppDbContext>();
        await AssertVersionAsync(db, first, 2);
        await AssertVersionAsync(db, second, 2);
    }

    [Fact]
    public async Task RunAsync_CancelledDuringWrite_RollsBackAndStopsTheScan()
    {
        // Arrange
        await using var host = await OwnDatabaseWebHost.StartAsync();
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var first = await SeedTailAsync(host, services);
        var second = await SeedTailAsync(host, services);
        using var pause = services.GetRequiredService<AppCommandGate>().PauseAt(HeadUpdate());
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var job = services.GetRequiredKeyedService<IRecurringJob>("IdleDraftCheckpoint");

        // Act
        var scanning = job.RunAsync(stopping.Token);
        await pause.Reached.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        await stopping.CancelAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanning);
        var db = services.GetRequiredService<AppDbContext>();
        await AssertVersionAsync(db, first, 2);
        await AssertVersionAsync(db, second, 2);
    }

    [GeneratedRegex(@"UPDATE\s+[\[""]FormDrafts", RegexOptions.IgnoreCase)]
    private static partial Regex HeadUpdate();

    private static async Task<FormDraft> SeedTailAsync(OwnDatabaseWebHost host, IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var tenant = await IntegrationTenants.InsertAsync(host.Database, Ct);
        var form = Form.Create(new FormCreateArgs(tenant, "Checkpoint"));
        db.Forms.Add(form);
        await db.SaveChangesAsync(Ct);
        var head = FormDraft.For(form, "{}");
        var batch = new DraftJournalBatch(1, "editor", ["{}"], """{"title":"private draft"}""", new string('a', 64));
        var changes = head.Append(batch, DraftWrite.System(DateTime.UnixEpoch), 0).Value;
        await services.GetRequiredService<IFormDraftRepository>().SaveOrConcedeAsync(head, changes, Ct);
        db.ChangeTracker.Clear();
        return head;
    }

    private static async Task AssertVersionAsync(AppDbContext db, FormDraft original, long version)
    {
        db.Database.CurrentTransaction.Should().BeNull();
        var head = await db.FormDrafts.AsNoTracking().SingleAsync(head => head.FormId == original.FormId, Ct);
        head.Version.Should().Be(version);
        head.JsonData.Should().Be(original.JsonData);
        head.ContentHash.Should().Be(original.ContentHash);
        if (version == original.Version)
        {
            head.ModifiedAt.Should().Be(original.ModifiedAt);
        }
        var history = db.FormDraftOperations.Where(row => row.FormId == original.FormId && row.TenantId == original.TenantId);
        (await history.CountAsync(Ct)).Should().Be((int)version);
        (await history.CountAsync(row => row.Reason == "idle", Ct)).Should().Be((int)version - 2);
    }
}
