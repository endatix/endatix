using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Verifies resource GETs return RFC7807 problem+json for missing entities (API 0.7.5 error shape).
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class FormNotFoundProblemDetailsFlowTests
{
    private const string SeedPassword = "Password123!";
    private readonly EndatixIntegrationWebHostFixture _fixture;

    public FormNotFoundProblemDetailsFlowTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetById_MissingForm_Returns404ProblemJson()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            cancellationToken);
        using HttpClient client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);
        const long missingFormId = 9_999_999_999L;

        // Act
        using HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/forms/{missingFormId}", UriKind.Relative),
            cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemDetailsAsync(response, expectedStatus: 404, cancellationToken);
    }

    [Fact]
    public async Task ListDefinitions_MissingForm_Returns404ProblemJson()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            cancellationToken);
        using HttpClient client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);
        const long missingFormId = 9_999_999_999L;

        // Act
        using HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/forms/{missingFormId}/definitions", UriKind.Relative),
            cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemDetailsAsync(response, expectedStatus: 404, cancellationToken);
    }

    private static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response,
        int expectedStatus,
        CancellationToken cancellationToken)
    {
        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        Assert.True(
            mediaType is "application/problem+json",
            $"Expected application/problem+json content type, got '{mediaType}'.");

        using JsonDocument? document = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.NotNull(document);

        JsonElement root = document.RootElement;
        Assert.True(root.TryGetProperty("status", out JsonElement statusElement), "ProblemDetails.status missing.");
        Assert.Equal(expectedStatus, statusElement.GetInt32());

        // Both members must be present AND non-empty. Asserting them separately matters:
        // an `||` short-circuits on a present-but-empty `detail` and never checks `title`.
        Assert.True(root.TryGetProperty("title", out JsonElement titleElement), "ProblemDetails.title missing.");
        Assert.False(
            string.IsNullOrWhiteSpace(titleElement.GetString()),
            "ProblemDetails.title must not be empty.");

        Assert.True(root.TryGetProperty("detail", out JsonElement detailElement), "ProblemDetails.detail missing.");
        Assert.False(
            string.IsNullOrWhiteSpace(detailElement.GetString()),
            "ProblemDetails.detail must not be empty; ToProblem falls back to the title.");
    }
}
