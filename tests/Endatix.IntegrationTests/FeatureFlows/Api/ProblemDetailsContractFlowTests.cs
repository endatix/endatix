using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Pins the RFC7807 contract on the paths the per-feature flow tests do not reach: unhandled
/// exceptions, the streaming export writer, authentication/authorization rejections, and a
/// domain conflict.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class ProblemDetailsContractFlowTests
{
    private const string SeedPassword = "Password123!";
    private const long MissingFormId = 9_999_999_999L;

    private static readonly string[] VolatileMembers = ["traceId"];

    private readonly EndatixIntegrationWebHostFixture _fixture;

    public ProblemDetailsContractFlowTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// The headline guarantee: a fault that nothing catches still answers with the canonical
    /// envelope, and the exception text never reaches the caller.
    /// </summary>
    [Fact]
    public async Task UnhandledException_ReturnsGenericProblemAndLeaksNothing()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using HttpClient client = await CreateClientAsync(TestPersona.TenantAdmin, cancellationToken);

        // Act
        using HttpResponseMessage response = await client.GetAsync(
            new Uri(UnhandledExceptionRouteStartupFilter.Path, UriKind.Relative),
            cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.DoesNotContain(
            UnhandledExceptionRouteStartupFilter.SensitiveMarker,
            body,
            StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);

        string expected = ProblemDetailsJson.Shape(
            $$"""
            {
              "type": "https://www.rfc-editor.org/rfc/rfc9110.html#name-500-internal-server-error",
              "title": "An unexpected error occurred",
              "status": 500,
              "detail": "An unexpected error occurred",
              "instance": "{{UnhandledExceptionRouteStartupFilter.Path}}",
              "traceId": "<string>"
            }
            """,
            VolatileMembers);

        string actual = await ProblemDetailsJson.ReadShapeAsync(response, cancellationToken, VolatileMembers);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// The export endpoint writes its own body instead of returning a typed result, so it is the
    /// one path where the problem+json content type can be lost to WriteAsJsonAsync's default.
    /// </summary>
    [Fact]
    public async Task Export_ForMissingForm_ReturnsCanonicalProblemJson()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using HttpClient client = await CreateClientAsync(TestPersona.TenantAdmin, cancellationToken);

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/forms/{MissingFormId}/submissions/export",
            new { },
            cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string expected = ProblemDetailsJson.Shape(
            $$"""
            {
              "type": "https://www.rfc-editor.org/rfc/rfc9110.html#name-400-bad-request",
              "title": "Export failed",
              "status": 400,
              "detail": "Form with ID {{MissingFormId}} not found",
              "instance": "/api/forms/{{MissingFormId}}/submissions/export",
              "traceId": "<string>"
            }
            """,
            VolatileMembers);

        string actual = await ProblemDetailsJson.ReadShapeAsync(response, cancellationToken, VolatileMembers);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// A domain conflict must carry its message: 4xx detail is author-written and actionable,
    /// unlike 5xx which is deliberately generic.
    /// </summary>
    [Fact]
    public async Task PartialUpdateForm_PublicSingleSubmission_ReturnsCanonicalConflictProblem()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using HttpClient client = await CreateClientAsync(TestPersona.TenantAdmin, cancellationToken);
        string formId = await CreateFormAsync(client, cancellationToken);

        // Act - a single-submission form cannot also be public.
        using HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/forms/{formId}",
            new { isPublic = true, limitOnePerUser = true },
            cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        string expected = ProblemDetailsJson.Shape(
            $$"""
            {
              "type": "https://www.rfc-editor.org/rfc/rfc9110.html#name-409-conflict",
              "title": "There was a conflict",
              "status": 409,
              "detail": "A single-submission form cannot be made public.",
              "instance": "/api/forms/{{formId}}",
              "traceId": "<string>"
            }
            """,
            VolatileMembers);

        string actual = await ProblemDetailsJson.ReadShapeAsync(response, cancellationToken, VolatileMembers);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Documents a known gap: 401 and 403 are produced by the authentication and authorization
    /// middleware, which never reaches <c>EndatixProblemDetails</c>. They answer with an empty
    /// body, so they are the two statuses outside the canonical envelope. If this test starts
    /// failing because a body appeared, bring that body into the envelope rather than relaxing
    /// the assertion.
    /// </summary>
    [Theory]
    [InlineData("/api/forms", HttpStatusCode.Unauthorized, false)]
    [InlineData("/api/admin/tenants", HttpStatusCode.Forbidden, true)]
    public async Task AuthRejections_AreOutsideTheProblemDetailsEnvelope(
        string path,
        HttpStatusCode expectedStatus,
        bool authenticated)
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await PrepareWorldAsync(cancellationToken);
        using HttpClient client = authenticated
            ? await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken)
            : world.AnonymousClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);

        // Assert
        Assert.Equal(expectedStatus, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(
            string.IsNullOrWhiteSpace(body),
            $"Expected an empty body for {(int)expectedStatus}, got: {body}");
    }

    private async Task<IntegrationTestWorld> PrepareWorldAsync(CancellationToken cancellationToken) =>
        await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            cancellationToken);

    private async Task<HttpClient> CreateClientAsync(TestPersona persona, CancellationToken cancellationToken)
    {
        IntegrationTestWorld world = await PrepareWorldAsync(cancellationToken);
        return await world.AsAsync(persona, cancellationToken: cancellationToken);
    }

    private static async Task<string> CreateFormAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/forms",
            new
            {
                name = "Problem details conflict fixture",
                isEnabled = true,
                formDefinitionJsonData = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q1"}]}]}"""
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using JsonDocument? document = await created.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.NotNull(document);

        string? id = document.RootElement.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(id), "Created form did not return an id.");

        return id!;
    }
}
