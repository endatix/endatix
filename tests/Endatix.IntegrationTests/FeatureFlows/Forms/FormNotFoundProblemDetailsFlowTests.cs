using System.Net;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Pins the RFC7807 problem+json body returned for missing entities. The whole envelope is
/// compared against a literal, so the error shape is visible in the test and any extra or
/// renamed member fails here.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class FormNotFoundProblemDetailsFlowTests
{
    private const string SeedPassword = "Password123!";
    private const long MissingFormId = 9_999_999_999L;

    /// <summary>`traceId` is per-request; everything else is ours and asserted verbatim.</summary>
    private static readonly string[] VolatileMembers = ["traceId"];

    private readonly EndatixIntegrationWebHostFixture _fixture;

    public FormNotFoundProblemDetailsFlowTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetById_MissingForm_ReturnsCanonicalNotFoundProblem()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using HttpClient client = await CreateTenantAdminClientAsync(cancellationToken);

        // Act
        using HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/forms/{MissingFormId}", UriKind.Relative),
            cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        string expected = ProblemDetailsJson.Shape(
            $$"""
            {
              "type": "https://www.rfc-editor.org/rfc/rfc9110#name-404-not-found",
              "title": "Resource not found",
              "status": 404,
              "detail": "Form not found.",
              "instance": "/api/forms/{{MissingFormId}}",
              "traceId": "<string>"
            }
            """,
            VolatileMembers);

        string actual = await ProblemDetailsJson.ReadShapeAsync(response, cancellationToken, VolatileMembers);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ListDefinitions_MissingForm_ReturnsCanonicalNotFoundProblem()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using HttpClient client = await CreateTenantAdminClientAsync(cancellationToken);

        // Act
        using HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/forms/{MissingFormId}/definitions", UriKind.Relative),
            cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        string expected = ProblemDetailsJson.Shape(
            $$"""
            {
              "type": "https://www.rfc-editor.org/rfc/rfc9110#name-404-not-found",
              "title": "Resource not found",
              "status": 404,
              "detail": "Form not found.",
              "instance": "/api/forms/{{MissingFormId}}/definitions",
              "traceId": "<string>"
            }
            """,
            VolatileMembers);

        string actual = await ProblemDetailsJson.ReadShapeAsync(response, cancellationToken, VolatileMembers);

        Assert.Equal(expected, actual);
    }

    private async Task<HttpClient> CreateTenantAdminClientAsync(CancellationToken cancellationToken)
    {
        IntegrationTestWorld world = await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            cancellationToken);

        return await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);
    }
}
