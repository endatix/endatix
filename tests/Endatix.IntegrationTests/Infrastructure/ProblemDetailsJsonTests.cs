using Endatix.IntegrationTests.Infrastructure;

namespace Endatix.IntegrationTests;

/// <summary>
/// Unit tests for the shape helper. No host or database - the flow tests that depend on it
/// cannot run without infrastructure, so its behaviour is pinned here.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProblemDetailsJsonTests
{
    [Fact]
    public void Shape_IsInsensitiveToMemberOrderAndWhitespace()
    {
        string a = ProblemDetailsJson.Shape("""{"status":404,"title":"Resource not found"}""");
        string b = ProblemDetailsJson.Shape("""
            {
              "title": "Resource not found",
              "status": 404
            }
            """);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Shape_RedactsVolatileScalarButKeepsTheMember()
    {
        string actual = ProblemDetailsJson.Shape(
            """{"detail":"Form not found.","traceId":"00-abc-def-01"}""",
            "traceId");

        Assert.Contains("\"traceId\": \"<string>\"", actual);
        Assert.Contains("\"detail\": \"Form not found.\"", actual);
        Assert.DoesNotContain("00-abc-def-01", actual);
    }

    [Fact]
    public void Shape_RedactsNestedMessagesButKeepsFieldKeys()
    {
        string actual = ProblemDetailsJson.Shape(
            """{"fields":{"Name":["'Name' must not be empty.","too short"]}}""",
            "fields");

        Assert.Contains("\"Name\"", actual);
        Assert.DoesNotContain("must not be empty", actual);
        Assert.DoesNotContain("too short", actual);
        Assert.Equal(2, actual.Split("<string>").Length - 1);
    }

    [Fact]
    public void Shape_DiffersWhenAnUnexpectedMemberIsPresent()
    {
        string canonical = ProblemDetailsJson.Shape("""{"status":400,"title":"Bad"}""");
        string withLegacyMember = ProblemDetailsJson.Shape(
            """{"status":400,"title":"Bad","errors":{"Name":["x"]}}""");

        // The exact-match assertion is what catches a resurrected FastEndpoints ErrorResponse.
        Assert.NotEqual(canonical, withLegacyMember);
    }

    [Fact]
    public void Shape_NormalizesWindowsNewlinesSoMultiLineDetailIsOsIndependent()
    {
        string crlf = ProblemDetailsJson.Shape("{\"detail\":\"first\\r\\nsecond\"}");
        string lf = ProblemDetailsJson.Shape("{\"detail\":\"first\\nsecond\"}");

        Assert.Equal(lf, crlf);
    }

    [Fact]
    public void Shape_LeavesNonStringValuesIntact()
    {
        string actual = ProblemDetailsJson.Shape("""{"status":404,"flag":true,"nothing":null}""");

        Assert.Contains("\"status\": 404", actual);
        Assert.Contains("\"flag\": true", actual);
        Assert.Contains("\"nothing\": null", actual);
    }
}
