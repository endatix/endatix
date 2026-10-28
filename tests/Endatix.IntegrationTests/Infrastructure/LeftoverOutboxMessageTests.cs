using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Persistence;
using Endatix.Outbox.Engine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.IntegrationTests;

/// <summary>
/// A test that ends with an outbox message pending leaves it for the next test's reset to clear. The shared host's
/// relay must not hand it to its handler meanwhile: a handler holds one table while it waits for the next, and the reset
/// locks every table one at a time in an order of its own, so the two can deadlock. Each test forces that interleaving,
/// pausing the handler where it would hold its first table, and resets.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class LeftoverOutboxMessageTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private const string FormDefinition = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q1"}]}]}""";

    // Lets the reset take its first locks before a paused handler would go on.
    private static readonly TimeSpan ResetHeadStart = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan ResetWait = TimeSpan.FromSeconds(30);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_form_deleted_message_left_pending_is_not_handled_while_the_next_test_resets()
    {
        // Arrange — the people cleanup would pause at its second statement, holding the table of its first.
        var admin = await TenantAdminAsync();
        var formId = await CreateFormAsync(admin);
        using var person = await admin.PostAsJsonAsync($"/api/forms/{formId}/audience/people", new { identifier = "ada@example.com" }, Ct);
        Assert.Equal(HttpStatusCode.Created, person.StatusCode);
        using var handler = Gate.PauseAt(AudienceStatements.MembershipsSoftDelete());
        using var deleted = await admin.DeleteAsync($"/api/forms/{formId}", Ct);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        // Act
        var outcome = await ResetWithMessageLeftAsync(handler);

        // Assert
        AssertLeftAlone(outcome, FormDeletedEvent.EventTypeName);
        Assert.False(await AnyAudienceRowAsync(), "The reset left audience rows behind.");
    }

    [Fact]
    public async Task A_test_mark_message_left_pending_is_not_handled_while_the_next_test_resets()
    {
        // Arrange — the link release would pause at its second statement, holding the submission's row.
        var admin = await TenantAdminAsync();
        var formId = await CreateFormAsync(admin);
        using var created = await admin.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions", new { isComplete = false, jsonData = "{}", currentPage = 0 }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var submissionId = await IdAsync(created);
        using var handler = Gate.PauseAt(AudienceStatements.LinkRelease());
        using var marked = await admin.PatchAsJsonAsync(
            $"/api/forms/{formId}/submissions/{submissionId}/test", new { isTest = true }, Ct);
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);

        // Act
        var outcome = await ResetWithMessageLeftAsync(handler);

        // Assert
        AssertLeftAlone(outcome, SubmissionUpdatedEvent.EventTypeName);
        Assert.False(await AnyAudienceRowAsync(), "The reset left audience rows behind.");
    }

    private AppCommandGate Gate => fixture.Factory.Services.GetRequiredService<AppCommandGate>();

    /// <summary>
    /// Gives the relay two polls to pick the message up, reads what it did to the outbox, then starts the next test's
    /// reset and only then lets a paused handler go on.
    /// </summary>
    private async Task<LeftoverOutcome> ResetWithMessageLeftAsync(CommandPause handler)
    {
        var poll = fixture.Factory.Services.GetRequiredService<IOptions<OutboxOptions>>().Value.PollInterval;
        var handledBeforeReset = await Task.WhenAny(handler.Reached, Task.Delay((poll * 2) + TimeSpan.FromSeconds(1), Ct)) == handler.Reached;
        var messages = await OutboxMessagesAsync();

        var reset = Task.Run(() => fixture.PrepareWorldAsync(IntegrationWorldOptions.Empty, Ct), Ct);
        await Task.Delay(ResetHeadStart, Ct);
        handler.Release();
        var resetFailure = await Record.ExceptionAsync(() => reset.WaitAsync(ResetWait, Ct));

        return new LeftoverOutcome(resetFailure, handledBeforeReset || handler.Reached.IsCompleted, messages);
    }

    private static void AssertLeftAlone(LeftoverOutcome outcome, string eventType)
    {
        Assert.Null(outcome.ResetFailure);
        Assert.False(outcome.Handled, $"The relay handed the {eventType} message the previous test left to its handler.");
        Assert.Contains(outcome.Messages, message => message.EventType == eventType);
        Assert.All(outcome.Messages, message => Assert.Equal((OutboxMessageStatus.Pending, null), (message.Status, message.LockedBy)));
    }

    private async Task<List<LeftMessage>> OutboxMessagesAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Select(message => new LeftMessage(message.EventType, message.Status, message.LockedBy))
            .ToListAsync(Ct);
    }

    private async Task<bool> AnyAudienceRowAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        return await db.Members.IgnoreQueryFilters().AnyAsync(Ct)
            || await db.Memberships.IgnoreQueryFilters().AnyAsync(Ct)
            || await db.Properties.IgnoreQueryFilters().AnyAsync(Ct)
            || await db.PropertyValues.IgnoreQueryFilters().AnyAsync(Ct)
            || await db.Links.IgnoreQueryFilters().AnyAsync(Ct);
    }

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
            new { name = $"Leftover {Guid.NewGuid():N}", isEnabled = true, formDefinitionJsonData = FormDefinition },
            Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await IdAsync(response);
    }

    private static async Task<long> IdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return long.Parse(body.GetProperty("id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed record LeftMessage(string EventType, OutboxMessageStatus Status, string? LockedBy);

    private sealed record LeftoverOutcome(Exception? ResetFailure, bool Handled, List<LeftMessage> Messages);
}
