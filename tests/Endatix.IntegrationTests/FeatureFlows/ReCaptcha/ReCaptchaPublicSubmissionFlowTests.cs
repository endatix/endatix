using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Features.ReCaptcha;
using Endatix.Framework.Logging;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.Logging;

namespace Endatix.IntegrationTests;

/// <summary>
/// An anonymous completion of a public form, verified against a recorded Google siteverify body.
/// The client always gets the same generic message; the log names the cause (endatix#1546).
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class ReCaptchaPublicSubmissionFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    [Theory]
    [InlineData("v2-test-key.json", 0.5, HttpStatusCode.BadRequest, LogLevel.Warning, EndatixEventIds.ReCaptcha.ActionMissing)]
    public async Task Public_completion_logs_the_reason_for_a_recorded_siteverify_body(
        string fixtureName, double minimumScore, HttpStatusCode expectedStatus, LogLevel expectedLevel, int expectedEventId)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = new ReCaptchaWebHost(fixture, SiteverifyFixtures.Body(fixtureName));
        var world = await host.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = "Password123!" },
            cancellationToken);
        string formId;
        using (var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken))
        {
            formId = await CreatePublicFormAsync(admin, cancellationToken);
        }

        host.Enable(world.Tenants[0].Id, minimumScore);
        using var respondent = host.CreateClient();

        // Act
        using var response = await respondent.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions",
            new { isComplete = true, jsonData = """{"q":"done"}""", currentPage = 0, reCaptchaToken = "recorded-token" },
            cancellationToken);

        // Assert
        Assert.Equal(expectedStatus, response.StatusCode);
        if (expectedStatus == HttpStatusCode.BadRequest)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Assert.Contains(ReCaptchaErrors.Messages.RECAPTCHA_VERIFICATION_FAILED, body);
        }

        Assert.Single(host.SiteverifyRequests);
        Assert.Equal([(expectedLevel, expectedEventId)], host.Events.Entries);
    }

    private static async Task<string> CreatePublicFormAsync(HttpClient admin, CancellationToken cancellationToken)
    {
        using var created = await admin.PostAsJsonAsync(
            "/api/forms",
            new
            {
                name = $"recaptcha-form-{Guid.NewGuid():N}",
                isEnabled = true,
                formDefinitionJsonData = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q"}]}]}"""
            },
            cancellationToken);
        created.EnsureSuccessStatusCode();
        await using var stream = await created.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var formId = document.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Form create response missing id.");

        using var madePublic = await admin.PatchAsJsonAsync($"/api/forms/{formId}", new { isPublic = true }, cancellationToken);
        madePublic.EnsureSuccessStatusCode();
        return formId;
    }
}

/// <summary>The recorded siteverify bodies, linked from Endatix.Infrastructure.Tests.</summary>
internal static class SiteverifyFixtures
{
    public static string Body(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Features", "ReCaptcha", "Fixtures", "Siteverify", name));
}
