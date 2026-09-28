using Endatix.Api.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api;

/// <summary>
/// Stable <c>errorCode</c> values Hub understands on a problem response.
/// </summary>
public static class EndatixProblemCodes
{
    /// <summary>
    /// Respondent cannot open the public form. Hub shows <c>title</c> and <c>detail</c>.
    /// </summary>
    public const string FORM_UNAVAILABLE = "form_unavailable";
}

/// <summary>
/// Writes the canonical Endatix problem envelope. Use this from customization middleware
/// instead of building the JSON by hand.
/// </summary>
public static class ProblemDetailsHttpExtensions
{
    /// <summary>
    /// Sets the status and writes RFC7807 <c>application/problem+json</c>
    /// (<c>type</c>, <c>title</c>, <c>status</c>, <c>detail</c>, <c>instance</c>, <c>traceId</c>, optional <c>errorCode</c>).
    /// A 5xx <paramref name="detail"/> is replaced with the generic title.
    /// Does nothing when the response has already started.
    /// </summary>
    public static async Task WriteEndatixProblemAsync(
        this HttpContext httpContext,
        int statusCode,
        string? title,
        string? detail,
        string? errorCode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Response.HasStarted)
        {
            return;
        }

        var problem = EndatixProblemDetails.Create(
            statusCode: statusCode,
            title: title,
            detail: detail,
            httpContext: httpContext,
            errorCode: errorCode);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: HttpConstants.ContentType.ProblemDetails,
            cancellationToken);
    }
}
