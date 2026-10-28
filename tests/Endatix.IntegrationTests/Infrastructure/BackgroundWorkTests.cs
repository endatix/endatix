using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>
/// Stopping a test host's <see cref="BackgroundWork"/> returns only once the work already running has finished, so the
/// next test's reset meets none of it.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class BackgroundWorkTests(EndatixIntegrationWebHostFixture fixture) : IAsyncDisposable
{
    private const string SeedPassword = "Password123!";
    private const string FormDefinition = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q1"}]}]}""";
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    // How long a stop is given to return while the work it should wait for is still held.
    private static readonly TimeSpan StopProbe = TimeSpan.FromSeconds(2);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Stopping_waits_for_the_outbox_handler_running_and_the_mark_that_its_message_was_sent()
    {
        // Arrange — the relay runs the form's people cleanup, held at its second statement.
        var admin = await TenantAdminAsync();
        var formId = await CreateFormAsync(admin);
        using var handler = Gate.PauseAt(AudienceStatements.MembershipsSoftDelete());
        await fixture.BackgroundWork.StartAsync(Ct);
        using var deleted = await admin.DeleteAsync($"/api/forms/{formId}", Ct);
        await handler.Reached.WaitAsync(Patience, Ct);

        // Act
        var stopping = fixture.BackgroundWork.StopAsync(Ct);
        var stoppedWhileHeld = await Task.WhenAny(stopping, Task.Delay(StopProbe, Ct)) == stopping;
        handler.Release();
        await stopping.WaitAsync(Patience, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.False(stoppedWhileHeld, "The stop returned while the relay's handler was still in its transaction.");
        Assert.Equal(OutboxMessageStatus.Sent, await StatusOfAsync(FormDeletedEvent.EventTypeName));
    }

    [Fact]
    public async Task Stopping_waits_for_the_job_running()
    {
        // Arrange — an import's Check job is held as it saves the counts of the chunk it checked.
        var admin = await TenantAdminAsync();
        var formId = await CreateFormAsync(admin);
        using var check = Gate.PauseAt(AudienceStatements.ImportCheckProgressSave());
        await fixture.BackgroundWork.StartAsync(Ct);
        var importUrl = await StageImportAsync(admin, formId);
        await check.Reached.WaitAsync(Patience, Ct);

        // Act
        var stopping = fixture.BackgroundWork.StopAsync(Ct);
        var stoppedWhileHeld = await Task.WhenAny(stopping, Task.Delay(StopProbe, Ct)) == stopping;
        check.Release();
        await stopping.WaitAsync(Patience, Ct);

        // Assert
        Assert.False(stoppedWhileHeld, "The stop returned while the Check job was still running.");
        var import = await admin.GetFromJsonAsync<JsonElement>(importUrl, Ct);
        Assert.Equal("checked", import.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_stop_that_gives_up_waiting_for_the_relay_still_leaves_the_scheduler_in_standby()
    {
        // Arrange — the relay is held in a handler's transaction, which the stop will not see end.
        var admin = await TenantAdminAsync();
        var formId = await CreateFormAsync(admin);
        using var handler = Gate.PauseAt(AudienceStatements.MembershipsSoftDelete());
        await fixture.BackgroundWork.StartAsync(Ct);
        using var deleted = await admin.DeleteAsync($"/api/forms/{formId}", Ct);
        await handler.Reached.WaitAsync(Patience, Ct);
        using var giveUp = new CancellationTokenSource(StopProbe);

        // Act
        var stopFailure = await Record.ExceptionAsync(() => fixture.BackgroundWork.StopAsync(giveUp.Token));
        var status = (await SchedulerAsync()).Status;

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(stopFailure);
        Assert.Equal(SchedulerStatus.Standby, status);
    }

    [Fact]
    public async Task A_job_firing_while_background_work_is_off_is_vetoed_and_leaves_nothing_to_stop_the_next_job()
    {
        // Arrange — the scheduler runs with the switch off, as one whose acquisition slipped past the standby would.
        var admin = await TenantAdminAsync();
        await fixture.BackgroundWork.StartAsync(Ct);
        await fixture.BackgroundWork.StopAsync(Ct);
        var scheduler = await SchedulerAsync();
        var vetoedUrl = await StageImportAsync(admin, await CreateFormAsync(admin));
        var vetoedTrigger = await TriggerOfAsync(admin, vetoedUrl);
        await scheduler.Start(Ct);
        await WaitUntilAsync(async () => !await scheduler.Exists(vetoedTrigger, Ct));

        // Act
        await fixture.BackgroundWork.StartAsync(Ct);
        var checkedUrl = await StageImportAsync(admin, await CreateFormAsync(admin));
        await WaitUntilAsync(async () => await ImportStatusAsync(admin, checkedUrl) == "checked");

        // Assert
        Assert.Equal("checking", await ImportStatusAsync(admin, vetoedUrl));
        Assert.Equal("checked", await ImportStatusAsync(admin, checkedUrl));
    }

    [Fact]
    public async Task Delivering_the_outbox_in_the_test_while_the_relay_is_on_is_refused()
    {
        // Arrange
        await fixture.BackgroundWork.StartAsync(Ct);

        // Act
        var refused = await Record.ExceptionAsync(() => fixture.BackgroundWork.DeliverOutboxOnceAsync(Ct));

        // Assert
        Assert.IsType<InvalidOperationException>(refused);
    }

    /// <summary>Stops what a test started, even a failed one, before the next test resets the database.</summary>
    public async ValueTask DisposeAsync() => await fixture.BackgroundWork.StopAsync(CancellationToken.None);

    private AppCommandGate Gate => fixture.Factory.Services.GetRequiredService<AppCommandGate>();

    private async Task<HttpClient> TenantAdminAsync()
    {
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, Ct);
        return await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: Ct);
    }

    private static async Task<long> CreateFormAsync(HttpClient admin)
    {
        using var response = await admin.PostAsJsonAsync(
            "/api/forms",
            new { name = $"Background {Guid.NewGuid():N}", isEnabled = true, formDefinitionJsonData = FormDefinition },
            Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return long.Parse(Text(body.GetProperty("id")), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Starts a two-row import and sends its one chunk, which starts the Check job; returns the import's URL.</summary>
    private static async Task<string> StageImportAsync(HttpClient admin, long formId)
    {
        using var started = await admin.PostAsJsonAsync(
            $"/api/forms/{formId}/audience/imports",
            new { fileName = "people.csv", headers = new[] { "email" }, rowCount = 2, identifierColumn = "email" },
            Ct);
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        var importId = (await started.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("importId");
        var importUrl = $"/api/forms/{formId}/audience/imports/{Text(importId)}";
        var rows = new[] { "ada@example.com", "bob@example.com" }.Select(email => new { cells = new[] { email }, malformed = false });
        using var sent = await admin.PutAsJsonAsync($"{importUrl}/chunks/0", new { firstRowNumber = 2, rows }, Ct);
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        return importUrl;
    }

    private async Task<IScheduler> SchedulerAsync() =>
        await fixture.Factory.Services
            .GetRequiredKeyedService<ISchedulerFactory>(QuartzRegistration.SchedulerName)
            .GetScheduler(Ct);

    /// <summary>The scheduler trigger of the job that runs the import's current phase.</summary>
    private static async Task<TriggerKey> TriggerOfAsync(HttpClient admin, string importUrl)
    {
        var import = await admin.GetFromJsonAsync<JsonElement>(importUrl, Ct);
        var jobId = Text(import.GetProperty("jobId"));
        var job = await admin.GetFromJsonAsync<JsonElement>($"/api/jobs/{jobId}", Ct);
        return new TriggerKey(jobId, job.GetProperty("type").GetString()!);
    }

    private static async Task<string?> ImportStatusAsync(HttpClient admin, string importUrl) =>
        (await admin.GetFromJsonAsync<JsonElement>(importUrl, Ct)).GetProperty("status").GetString();

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Still not so after {Patience.TotalSeconds:0} s.");
            await Task.Delay(200, Ct);
        }
    }

    /// <summary>Ids may be sent as JSON strings (snowflake) or numbers.</summary>
    private static string Text(JsonElement id) => id.ValueKind == JsonValueKind.String ? id.GetString()! : id.GetRawText();

    private async Task<OutboxMessageStatus> StatusOfAsync(string eventType)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(message => message.EventType == eventType)
            .Select(message => message.Status)
            .SingleAsync(Ct);
    }
}
