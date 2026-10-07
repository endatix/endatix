using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Critical-path coverage for a public screen-out: the token save persists <c>screen_out</c> without completing,
/// captures <c>submission.collection_status_changed</c> to the outbox, and closes the token for later saves.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed class SubmissionScreenOutFlowTests
{
    private const string SeedPassword = "Password123!";
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly EndatixIntegrationWebHostFixture _fixture;

    public SubmissionScreenOutFlowTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Screen_out_by_token_persists_screen_out_and_rejects_the_next_token_save()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword },
            cancellationToken);

        var tenantId = world.Tenants[0].Id;
        await EnsureTenantSettingsAsync(world.Services, tenantId, cancellationToken);

        using var client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);
        var formId = await CreateEnabledFormAsync(client, cancellationToken);

        var createResponse = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions",
            new { isComplete = false, jsonData = """{"age":"18_or_over"}""", currentPage = 0 },
            cancellationToken);
        createResponse.EnsureSuccessStatusCode();
        var created = await ReadSubmissionAsync(createResponse, cancellationToken);
        Assert.False(string.IsNullOrEmpty(created.Token));

        // Act — the screen-out trigger ends the interview
        var screenOutResponse = await client.PatchAsJsonAsync(
            $"/api/forms/{formId}/submissions/by-token/{created.Token}",
            new { jsonData = """{"age":"under_18"}""", currentPage = 0, collectionOutcome = "screen_out" },
            cancellationToken);

        // Assert — stored as screen_out, not complete
        screenOutResponse.EnsureSuccessStatusCode();
        var screenedOut = await ReadSubmissionAsync(screenOutResponse, cancellationToken);
        Assert.Equal(CollectionStatusCodes.ScreenOut, screenedOut.CollectionStatus);
        Assert.False(screenedOut.IsComplete);
        Assert.Null(screenedOut.CompletedAt);

        var submissionId = long.Parse(created.Id);
        await using (var scope = world.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Submissions
                .AsNoTracking()
                .SingleAsync(row => row.Id == submissionId, cancellationToken);
            Assert.Equal(CollectionStatusCodes.ScreenOut, stored.CollectionStatus.Code);
            Assert.False(stored.IsComplete);
            Assert.Equal("under_18", AgeOf(stored.JsonData));

            // Payload is jsonb on PostgreSQL, so match the submission id in memory.
            var tenantMessages = await db.OutboxMessages
                .AsNoTracking()
                .Where(row => row.TenantId == tenantId)
                .Select(row => new { row.EventType, row.Payload })
                .ToListAsync(cancellationToken);
            var eventTypes = tenantMessages
                .Where(row => row.Payload.Contains(created.Id, StringComparison.Ordinal))
                .Select(row => row.EventType)
                .ToList();
            Assert.Single(eventTypes, type => type == SubmissionCollectionStatusChangedEvent.EventTypeName);
            Assert.DoesNotContain(SubmissionCompletedEvent.EventTypeName, eventTypes);
        }

        // Act — a later save with the same token
        var laterResponse = await client.PatchAsJsonAsync(
            $"/api/forms/{formId}/submissions/by-token/{created.Token}",
            new { jsonData = """{"age":"18_or_over"}""", currentPage = 1 },
            cancellationToken);

        // Assert — rejected, and the stored answers are unchanged
        Assert.Equal(HttpStatusCode.BadRequest, laterResponse.StatusCode);
        await using (var scope = world.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Submissions
                .AsNoTracking()
                .SingleAsync(row => row.Id == submissionId, cancellationToken);
            Assert.Equal("under_18", AgeOf(stored.JsonData));
            Assert.Equal(CollectionStatusCodes.ScreenOut, stored.CollectionStatus.Code);
        }
    }

    private static string? AgeOf(string jsonData)
    {
        using var document = JsonDocument.Parse(jsonData);
        return document.RootElement.GetProperty("age").GetString();
    }

    private static async Task EnsureTenantSettingsAsync(
        IServiceProvider services,
        long tenantId,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exists = await db.TenantSettings.AnyAsync(row => row.TenantId == tenantId, cancellationToken);
        if (!exists)
        {
            db.TenantSettings.Add(new TenantSettings(tenantId));
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<string> CreateEnabledFormAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/forms",
            new
            {
                name = $"screen-out-form-{Guid.NewGuid():N}",
                isEnabled = true,
                formDefinitionJsonData = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"age"}]}]}"""
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Form create response missing id.");
    }

    private static async Task<SubmissionApiModel> ReadSubmissionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var model = JsonSerializer.Deserialize<SubmissionApiModel>(bytes, _jsonOptions);
        Assert.NotNull(model);
        return model;
    }

    private sealed class SubmissionApiModel
    {
        public string Id { get; set; } = string.Empty;
        public bool IsComplete { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string CollectionStatus { get; set; } = string.Empty;
        public string? Token { get; set; }
    }
}
