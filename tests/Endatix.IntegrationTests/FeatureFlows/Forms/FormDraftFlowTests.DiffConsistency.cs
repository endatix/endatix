using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.FeatureFlows.Forms;

public sealed partial class FormDraftFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Comparison_reads_the_published_document_number_admission_and_draft_at_one_point(bool queueUpgrade)
    {
        // Arrange: the reader has seen the old active definition but has not checked admission or read the head.
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        await EditDraftAsync(editor, form);
        using var scope = fixture.Factory.Services.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gate = fixture.Factory.Services.GetRequiredService<AppCommandGate>();
        using var pause = gate.PauseAt(PendingUpgradeRead());
        var read = editor.GetAsync(form + DIFF, Cancellation);
        await pause.Reached.WaitAsync(TimeSpan.FromSeconds(20), Cancellation);

        // Act: commit both changes together (or observe the writer waiting for the SQL Server reader).
        await writer.Database.OpenConnectionAsync(Cancellation);
        var session = writer.Database.IsSqlServer() ? await SqlSessionAsync(writer) : 0;
        var write = ReplaceBothSidesAsync(writer, FormId(form), queueUpgrade);
        try
        {
            await WaitForCommitOrReadLockAsync(write, session);
        }
        finally
        {
            pause.Release();
            await Task.WhenAll(write, read);
        }

        // Assert: the paused comparison is wholly before the write; a new read observes the new state.
        using var original = await read;
        await AssertComparisonAsync(original, ("\"2\"", 1), ("Live", "Edited"));
        using var current = await editor.GetAsync(form + DIFF, Cancellation);
        if (queueUpgrade)
        {
            current.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            return;
        }
        await AssertComparisonAsync(current, ("\"3\"", 10), ("New live", "New draft"));
    }

    private static async Task ReplaceBothSidesAsync(AppDbContext db, long formId, bool queueUpgrade)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(Cancellation);
        var owner = await db.Forms.Include(f => f.ActiveDefinition).SingleAsync(f => f.Id == formId, Cancellation);
        var active = owner.ActiveDefinition!;
        active.Number.Should().BeNull();
        active.UpdateSchema("""{"title":"New live","pages":[]}""");
        await db.SaveChangesAsync(Cancellation);
        var numbered = new FormDefinition(owner.TenantId);
        owner.AddFormDefinition(numbered, isActive: false);
        db.Entry(numbered).Property(d => d.Number).CurrentValue = 9;
        var head = await db.FormDrafts.SingleAsync(d => d.FormId == formId, Cancellation);
        db.FormDraftOperations.AddRange(head.Replace("""{"title":"New draft","pages":[]}""", DraftWrite.System(DateTime.UtcNow)));
        if (queueUpgrade)
        {
            db.Add(new LegacyDraftUpgrade(formId, active.Id));
        }
        await db.SaveChangesAsync(Cancellation);
        await transaction.CommitAsync(Cancellation);
    }

    private async Task WaitForCommitOrReadLockAsync(Task write, int session)
    {
        if (session == 0)
        {
            await write.WaitAsync(TimeSpan.FromSeconds(20), Cancellation);
            return;
        }
        using var scope = fixture.Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        while (!write.IsCompleted && !await IsBlockedAsync(observer, session, timeout.Token))
        {
            await Task.Delay(25, timeout.Token);
        }
    }

    private static Task<int> SqlSessionAsync(AppDbContext writer) =>
        writer.Database.SqlQuery<int>($"SELECT CAST(@@SPID AS int) AS Value").SingleAsync(Cancellation);

    private static Task<bool> IsBlockedAsync(AppDbContext observer, int session, CancellationToken ct) =>
        observer.Database.SqlQuery<int>($"SELECT session_id AS Value FROM sys.dm_exec_requests WHERE session_id={session} AND blocking_session_id>0").AnyAsync(ct);

    private static async Task AssertComparisonAsync(HttpResponseMessage response, (string Tag, int Number) version, (string Live, string Draft) titles)
    {
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        var change = body.GetProperty("changes").EnumerateArray().Single();
        var observed = (response.Headers.ETag!.Tag, body.GetProperty("publishedNumber").GetInt32(),
            change.GetProperty("before").GetString(), change.GetProperty("after").GetString());
        observed.Should().Be((version.Tag, version.Number, titles.Live, titles.Draft));
    }

    [GeneratedRegex(@"FROM\s+[\[""]LegacyDraftUpgrades", RegexOptions.IgnoreCase)]
    private static partial Regex PendingUpgradeRead();
}
