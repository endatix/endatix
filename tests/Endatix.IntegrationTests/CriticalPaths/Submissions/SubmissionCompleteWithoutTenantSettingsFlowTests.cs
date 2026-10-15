using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.IntegrationTests.Shared;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed class SubmissionCompleteWithoutTenantSettingsFlowTests(
    EndatixIntegrationWebHostFixture fixture)
{
    [Fact]
    public async Task Completing_a_submission_without_tenant_settings_succeeds()
    {
        // Arrange — no TenantSettings row is inserted, which previously made
        // SlackNotificationHandler throw "Tenant settings must be configured."
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = "Password123!" },
            cancellationToken);

        using var client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);
        var formId = await CreateEnabledFormAsync(client, cancellationToken);

        // Act — complete on create, which raises SubmissionCompletedEvent
        var response = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions",
            new { isComplete = true, jsonData = "{}", currentPage = 0 },
            cancellationToken);

        // Assert — the write stays successful even though Slack cannot notify
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // and no TenantSettings row was created as a side effect
        await using var scope = world.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = world.Tenants[0].Id;
        Assert.False(await db.TenantSettings.AnyAsync(
            row => row.TenantId == tenantId, cancellationToken));
    }

    private static async Task<string> CreateEnabledFormAsync(
        HttpClient client, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/forms",
            new
            {
                name = $"no-settings-form-{Guid.NewGuid():N}",
                isEnabled = true,
                formDefinitionJsonData = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q"}]}]}"""
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Form create response missing id.");
    }
}
