using Endatix.Api;
using Microsoft.AspNetCore.Http;

namespace Endatix.Samples.SelfHosted;

/// <summary>
/// Sample only. Not registered by default.
/// Deny GET /api/public/forms/{formId}/access with the respondent problem Hub renders.
/// Register it before <c>app.UseEndatix()</c> so the response is written before the endpoint runs.
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

        var path = request.Path.Value ?? string.Empty;
        return path.Contains("/public/forms/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith("/access", StringComparison.OrdinalIgnoreCase);
    }
}
