using System.Net.Mime;
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
        var pathBase = context.Request.PathBase.HasValue
            ? context.Request.PathBase.Value
            : string.Empty;

        if (bare && !parsedFormId)
        {
            await WritePlainAsync(context, StatusCodes.Status400BadRequest, "formId must be a positive integer.");
            return;
        }

        if (!EmbedHostPage.TryResolveHubBaseUrl(query["hubBaseUrl"], settings, out var hubBaseUrl))
        {
            if (bare)
            {
                await WritePlainAsync(context, StatusCodes.Status400BadRequest, "hubBaseUrl is missing or not allowlisted.");
                return;
            }

            _ = EmbedHostPage.TryResolveHubBaseUrl(null, settings, out hubBaseUrl);
            hubBaseUrl ??= new Uri("http://localhost:3000");
            await WriteBuilderAsync(
                context,
                formId: null,
                hubBaseUrl,
                error: "hubBaseUrl is missing or not allowlisted.",
                formIdField: hasFormId ? formIdRaw : null,
                pathBase);
            return;
        }

        if (!bare && hasFormId && !parsedFormId)
        {
            await WriteBuilderAsync(
                context,
                formId: null,
                hubBaseUrl,
                error: "formId must be a positive integer.",
                formIdField: formIdRaw,
                pathBase);
            return;
        }

        var heightMode = EmbedHostPage.NormalizeHeightMode(query["heightMode"]);
        var prefill = EmptyToNull(query["prefill"]);
        var token = EmptyToNull(query["token"]);
        var requestedHub = EmptyToNull(query["hubBaseUrl"]);
        var mixedContent = EmbedHostPage.IsMixedContent(context.Request.IsHttps, hubBaseUrl);
        var httpPlaygroundHref = EmbedHostPage.LocalHttpPlaygroundUrl(context.Request);

        var html = bare && !mixedContent
            ? EmbedHostPage.RenderBareHtml(formId, hubBaseUrl, heightMode, prefill, token)
            : EmbedHostPage.RenderBuilderHtml(
                parsedFormId ? formId : null,
                hubBaseUrl,
                heightMode,
                prefill,
                token,
                requestedHub,
                pathBase,
                mixedContent: mixedContent,
                httpPlaygroundHref: httpPlaygroundHref);

        await WriteHtmlAsync(context, html);
    }

    private static async Task WriteBuilderAsync(
        HttpContext context,
        string? formId,
        Uri hubBaseUrl,
        string error,
        string? formIdField,
        string pathBase)
    {
        var query = context.Request.Query;
        var html = EmbedHostPage.RenderBuilderHtml(
            formId,
            hubBaseUrl,
            EmbedHostPage.NormalizeHeightMode(query["heightMode"]),
            EmptyToNull(query["prefill"]),
            EmptyToNull(query["token"]),
            EmptyToNull(query["hubBaseUrl"]),
            pathBase,
            error,
            formIdField,
            mixedContent: EmbedHostPage.IsMixedContent(context.Request.IsHttps, hubBaseUrl),
            httpPlaygroundHref: EmbedHostPage.LocalHttpPlaygroundUrl(context.Request));
        await WriteHtmlAsync(context, html);
    }

    private static async Task WriteHtmlAsync(HttpContext context, string html)
    {
        context.Response.ContentType = MediaTypeNames.Text.Html + "; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Robots-Tag"] = "noindex";
        await context.Response.WriteAsync(html, context.RequestAborted);
    }

    private static async Task WritePlainAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = MediaTypeNames.Text.Plain + "; charset=utf-8";
        await context.Response.WriteAsync(message, context.RequestAborted);
    }

    private static string? EmptyToNull(StringValues value)
    {
        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
