using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Events;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// One fact, one event. Screen-out writes the collection event. Complete writes
/// submission.completed and does not write the collection event. An in-progress
/// save writes neither.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed class SubmissionCollectionStatusEventFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";

    [Fact]
    public async Task ScreenOut_WritesCollectionEvent_CompleteAndProgressDoNot()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword },
            cancellationToken);
        using HttpClient client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);
        string formId = await CreateEnabledFormAsync(client, cancellationToken);

        string screenedId = await CreateSubmissionAsync(client, formId, isComplete: false, cancellationToken);
        HttpResponseMessage screenOut = await client.PatchAsJsonAsync(
            $"/api/forms/{formId}/submissions/{screenedId}",
            new { isComplete = false, jsonData = """{"age":"under_18"}""", collectionOutcome = "screen_out" },
            cancellationToken);
        screenOut.EnsureSuccessStatusCode();

        string openId = await CreateSubmissionAsync(client, formId, isComplete: false, cancellationToken);
        HttpResponseMessage progress = await client.PatchAsJsonAsync(
            $"/api/forms/{formId}/submissions/{openId}",
            new { isComplete = false, jsonData = """{"age":"18_or_over"}""" },
            cancellationToken);
        progress.EnsureSuccessStatusCode();

        string completedId = await CreateSubmissionAsync(client, formId, isComplete: true, cancellationToken);

        await using AsyncServiceScope scope = world.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        List<string> eventTypes = await db.OutboxMessages
            .Select(message => message.EventType + " " + message.Payload)
            .ToListAsync(cancellationToken);

        Assert.Contains(
            eventTypes,
            row => row.StartsWith(SubmissionCollectionStatusChangedEvent.EventTypeName, StringComparison.Ordinal)
                && row.Contains(screenedId, StringComparison.Ordinal));
        Assert.DoesNotContain(
            eventTypes,
            row => row.StartsWith(SubmissionCollectionStatusChangedEvent.EventTypeName, StringComparison.Ordinal)
                && (row.Contains(openId, StringComparison.Ordinal) || row.Contains(completedId, StringComparison.Ordinal)));
        Assert.Contains(
            eventTypes,
            row => row.StartsWith(SubmissionCompletedEvent.EventTypeName, StringComparison.Ordinal)
                && row.Contains(completedId, StringComparison.Ordinal));
        Assert.DoesNotContain(
            eventTypes,
            row => row.StartsWith(SubmissionCompletedEvent.EventTypeName, StringComparison.Ordinal)
                && row.Contains(screenedId, StringComparison.Ordinal));
    }

    private static async Task<string> CreateEnabledFormAsync(HttpClient client, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/forms",
            new
            {
                name = $"screen-out-events-{Guid.NewGuid():N}",
                isEnabled = true,
                formDefinitionJsonData = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"age"}]}]}"""
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Form create response missing id.");
    }

    private static async Task<string> CreateSubmissionAsync(
        HttpClient client,
        string formId,
        bool isComplete,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions",
            new { isComplete, jsonData = "{}", currentPage = 0 },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Submission create response missing id.");
    }
}
