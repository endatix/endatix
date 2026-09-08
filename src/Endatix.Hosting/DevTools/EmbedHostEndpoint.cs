using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Endatix.Hosting.DevTools;

internal static class EmbedHostEndpoint
{
    public static async Task ExecuteAsync(HttpContext context)
    {
        var settings = context.RequestServices.GetService<IOptions<EmbedHostOptions>>()?.Value
            ?? new EmbedHostOptions();

        if (!settings.IsEnabled)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var query = context.Request.Query;
        var formIdRaw = query["formId"].ToString();
        var hasFormId = !string.IsNullOrWhiteSpace(formIdRaw);
        var parsedFormId = EmbedHostPage.TryParseFormId(formIdRaw, out var formId);
        var bare = EmbedHostPage.IsBareView(query["view"]);

        if (!parsedFormId && (bare || hasFormId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("formId must be a positive integer.", context.RequestAborted);
            return;
        }

        if (!EmbedHostPage.TryResolveHubBaseUrl(query["hubBaseUrl"], settings, out var hubBaseUrl))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("hubBaseUrl is missing or not allowlisted.", context.RequestAborted);
            return;
        }

        var heightMode = EmbedHostPage.NormalizeHeightMode(query["heightMode"]);
        var prefill = EmptyToNull(query["prefill"]);
        var token = EmptyToNull(query["token"]);
        var requestedHub = EmptyToNull(query["hubBaseUrl"]);
        var resolvedFormId = parsedFormId ? formId : null;

        var html = bare
            ? EmbedHostPage.RenderBareHtml(formId, hubBaseUrl, heightMode, prefill, token)
            : EmbedHostPage.RenderBuilderHtml(
                resolvedFormId,
                hubBaseUrl,
                heightMode,
                prefill,
                token,
                requestedHub);

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        await context.Response.WriteAsync(html, context.RequestAborted);
    }

    private static string? EmptyToNull(StringValues value)
    {
        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
