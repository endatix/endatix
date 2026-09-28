using Endatix.Api;
using Microsoft.AspNetCore.Http;

namespace Endatix.Samples.SelfHosted;

/// <summary>
/// Sample only. Not registered by default.
/// Deny GET /api/public/forms/{formId}/access with the respondent problem Hub renders.
/// Register it before <c>app.UseEndatix()</c>. <c>ConfigureAdditionalMiddleware</c> runs after
/// <c>UseApi</c>, so it never sees those requests.
/// </summary>
public sealed class FormUnavailableSampleMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (IsPublicFormAccess(context.Request))
        {
            await context.WriteEndatixProblemAsync(
                StatusCodes.Status403Forbidden,
                title: "This survey is no longer available.",
                detail: "Thank you for your interest. Unfortunately, this survey can no longer be completed.",
                errorCode: EndatixProblemCodes.FORM_UNAVAILABLE,
                cancellationToken: context.RequestAborted);
            return;
        }

        await next(context);
    }

    internal static bool IsPublicFormAccess(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method))
        {
            return false;
        }

        var segments = (request.Path.Value ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 4)
        {
            return false;
        }

        var tail = segments[^4..];
        return string.Equals(tail[0], "public", StringComparison.OrdinalIgnoreCase)
            && string.Equals(tail[1], "forms", StringComparison.OrdinalIgnoreCase)
            && string.Equals(tail[3], "access", StringComparison.OrdinalIgnoreCase);
    }
}
