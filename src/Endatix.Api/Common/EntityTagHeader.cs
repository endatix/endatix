using System.Globalization;
using Endatix.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Common;

/// <summary>
/// The <c>ETag</c> and <c>If-Match</c> headers of a versioned write (the If-Match row of the ARCHITECTURE.md decision
/// log, endatix#1691): one strong tag holding the row's <c>Version</c>, <c>"3"</c>, and <c>"0"</c> before the first write.
/// </summary>
/// <remarks>
/// A write requires <c>If-Match</c>: missing is 428, a value that is not one strong tag is 400 (<see cref="ReadIfMatch"/>),
/// and a version that is no longer current is the endpoint's 412.
/// </remarks>
public static class EntityTagHeader
{
    /// <summary>The header value for <paramref name="version"/>: the number in quotes.</summary>
    public static string Format(long version) => $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>
    /// Reads one version from an <c>If-Match</c> value, a strong tag such as <c>"3"</c>. False for a missing value, a
    /// bare number (<c>3</c>, not an entity-tag), a weak tag (<c>W/"3"</c>, which If-Match cannot compare), <c>*</c>, a
    /// list, or anything not a number.
    /// </summary>
    public static bool TryParse(string? ifMatch, out long version)
    {
        version = 0;
        var value = ifMatch?.Trim();
        return value is { Length: > 2 } && value[0] == '"' && value[^1] == '"'
            && long.TryParse(value.AsSpan(1, value.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }

    /// <summary>
    /// Reads the version a write names in <c>If-Match</c>: null and the version when it is one strong tag, else the
    /// problem to answer, 428 when it is missing and 400 when it cannot be read.
    /// </summary>
    public static ProblemHttpResult? ReadIfMatch(string? ifMatch, out long version)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            version = 0;
            return Problem(StatusCodes.Status428PreconditionRequired, "Precondition required",
                "Send If-Match with the version you read, in quotes; \"0\" before the first write.");
        }

        return TryParse(ifMatch, out version)
            ? null
            : Problem(StatusCodes.Status400BadRequest, ResultExtensions.ResultTitles.BAD_REQUEST,
                "If-Match must hold one strong version tag, such as \"3\"; bare numbers, weak tags, * and lists are not accepted.");
    }

    /// <summary>Sets the response's <c>ETag</c> to <paramref name="version"/>.</summary>
    public static void SetETag(HttpResponse response, long version) =>
        response.Headers.ETag = Format(version);

    private static ProblemHttpResult Problem(int status, string title, string detail) =>
        TypedResults.Problem(EndatixProblemDetails.Create(status, title, detail));
}
