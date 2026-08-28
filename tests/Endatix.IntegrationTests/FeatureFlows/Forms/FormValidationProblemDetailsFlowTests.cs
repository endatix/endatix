using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Pins the RFC7807 problem+json body returned for FastEndpoints/FluentValidation failures.
/// The envelope must be identical to the one handler <c>ToProblem</c> produces, with the extra
/// <c>fields</c> dictionary - never the stock FastEndpoints <c>ErrorResponse</c>. Comparing the
/// whole document means a resurrected <c>statusCode</c> / <c>message</c> / <c>errors</c> member
/// fails here without a separate absence assertion.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class FormValidationProblemDetailsFlowTests
{
    private const string SeedPassword = "Password123!";

    /// <summary>
    /// `traceId` is per-request. `detail` and the messages under `fields` are FluentValidation's
    /// wording, which we do not own - the member and its structure are asserted, the prose is not.
    /// The `fields` keys are camelCase (`name`, not `Name`): FastEndpoints camelCases the
    /// validation property name, which is what a JSON client needs to map errors back to inputs.
    /// </summary>
    private static readonly string[] VolatileMembers = ["traceId", "detail", "fields"];

    /// <summary>Used where the messages themselves are the point of the test.</summary>
    private static readonly string[] TraceIdOnly = ["traceId"];

    private readonly EndatixIntegrationWebHostFixture _fixture;

    public FormValidationProblemDetailsFlowTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateForm_MissingName_ReturnsCanonicalValidationProblem()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            cancellationToken);
        using HttpClient client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);

        // Act - `name` omitted; everything else is valid so `Name` is the only failing rule.
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/forms",
            new
            {
                isEnabled = true,
                formDefinitionJsonData = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q1"}]}]}"""
            },
            cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string expected = ProblemDetailsJson.Shape(
            """
            {
              "type": "https://www.rfc-editor.org/rfc/rfc9110#name-400-bad-request",
              "title": "There was a problem with your request",
              "status": 400,
              "detail": "<string>",
              "instance": "/api/forms",
              "traceId": "<string>",
              "errorCode": "NotEmptyValidator",
              "fields": {
                "name": ["<string>"]
              }
            }
            """,
            VolatileMembers);

        string actual = await ProblemDetailsJson.ReadShapeAsync(response, cancellationToken, VolatileMembers);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Documents the multi-error shape end to end: several failing properties, and a property
    /// that fails two rules at once, all in one response.
    ///
    /// Unlike the single-error test above, the messages are asserted verbatim - this test is
    /// intended to be read as the reference example of the payload a client receives, so the
    /// wording is part of what it documents. A FluentValidation upgrade that rewords a built-in
    /// message will fail here by design; update the literal.
    ///
    /// Two things worth noticing in the expected body:
    ///   * `fields` keys are camelCase (`name`, `isEnabled`) EXCEPT `FormDefinition`, which is
    ///     PascalCase because the rule sets it explicitly via `WithName("FormDefinition")` and
    ///     FastEndpoints only camelCases names it derives itself. A client mapping `fields` keys
    ///     onto form inputs has to special-case it - see the note in the test body.
    ///   * `errorCode` carries only the FIRST failure's code, so it cannot be used to
    ///     discriminate between the individual field errors; `fields` is the machine-readable part.
    /// </summary>
    [Fact]
    public async Task CreateForm_MultipleInvalidFields_ReturnsEveryFailureGroupedByProperty()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            cancellationToken);
        using HttpClient client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);

        // Act - four properties fail; `name` fails two rules (NotEmpty and MinimumLength(2)),
        // `isEnabled` is omitted, the form definition is missing entirely, `metadata` is not JSON.
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/forms",
            new { name = "", metadata = "not-json" },
            cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string expected = ProblemDetailsJson.Shape(
            """
            {
              "type": "https://www.rfc-editor.org/rfc/rfc9110#name-400-bad-request",
              "title": "There was a problem with your request",
              "status": 400,
              "detail": "'name' must not be empty.\nThe length of 'name' must be at least 2 characters. You entered 0 characters.\n'is Enabled' must not be empty.\nEither FormDefinitionJsonData or FormDefinitionSchema must be provided.\nmetadata must be a valid JSON string.",
              "instance": "/api/forms",
              "traceId": "<string>",
              "errorCode": "NotEmptyValidator",
              "fields": {
                "name": [
                  "'name' must not be empty.",
                  "The length of 'name' must be at least 2 characters. You entered 0 characters."
                ],
                "isEnabled": [
                  "'is Enabled' must not be empty."
                ],
                "FormDefinition": [
                  "Either FormDefinitionJsonData or FormDefinitionSchema must be provided."
                ],
                "metadata": [
                  "metadata must be a valid JSON string."
                ]
              }
            }
            """,
            TraceIdOnly);

        string actual = await ProblemDetailsJson.ReadShapeAsync(response, cancellationToken, TraceIdOnly);

        Assert.Equal(expected, actual);

        // `detail` is every message joined by a newline, in rule-declaration order. It is a
        // human-readable summary only - clients should render `fields`, which preserves the
        // property grouping and the per-property ordering.
        using JsonDocument document = JsonDocument.Parse(actual);
        string detail = document.RootElement.GetProperty("detail").GetString()!;
        Assert.Equal(5, detail.Split('\n').Length);
    }
}
