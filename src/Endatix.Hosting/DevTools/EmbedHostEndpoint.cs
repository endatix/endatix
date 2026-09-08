using System.Net.Mime;
using Endatix.Core.Common;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        var pathBase = context.Request.PathBase.ToString();

        if (bare && !parsedFormId)
        {
            await WritePlainAsync(context, StatusCodes.Status400BadRequest, "formId must be a positive integer.");
            return;
        }

        if (!EmbedHostPage.TryResolveHubBaseUrl(query["hubBaseUrl"], settings, out var hubBaseUrl))
        {
            await WriteUnresolvedHubAsync(context, settings, bare, hasFormId, formIdRaw, pathBase);
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

        await WritePlaygroundAsync(context, formId, parsedFormId, hubBaseUrl, query, pathBase, bare);
    }

    private static async Task WriteUnresolvedHubAsync(
        HttpContext context,
        EmbedHostOptions settings,
        bool bare,
        bool hasFormId,
        string formIdRaw,
        string pathBase)
    {
        if (bare)
        {
            await WritePlainAsync(context, StatusCodes.Status400BadRequest, "hubBaseUrl is missing or not allowlisted.");
            return;
        }

        _ = EmbedHostPage.TryResolveHubBaseUrl(null, settings, out var hubBaseUrl);
        await WriteBuilderAsync(
            context,
            formId: null,
            hubBaseUrl ?? EmbedHostPage.FallbackHubOrigin,
            error: "hubBaseUrl is missing or not allowlisted.",
            formIdField: hasFormId ? formIdRaw : null,
            pathBase);
    }

    private static Task WritePlaygroundAsync(
        HttpContext context,
        string formId,
        bool parsedFormId,
        Uri hubBaseUrl,
        IQueryCollection query,
        string pathBase,
        bool bare)
    {
        var heightMode = EmbedHostPage.NormalizeHeightMode(query["heightMode"]);
        var prefill = query["prefill"].ToString().NullIfWhiteSpace();
        var token = query["token"].ToString().NullIfWhiteSpace();
        var requestedHub = query["hubBaseUrl"].ToString().NullIfWhiteSpace();
        var mixedContent = EmbedHostPage.IsMixedContent(context.Request.IsHttps, hubBaseUrl);

        if (bare && !mixedContent)
        {
            return WriteHtmlAsync(
                context,
                EmbedHostPage.RenderBareHtml(formId, hubBaseUrl, heightMode, prefill, token));
        }

        return WriteHtmlAsync(
            context,
            EmbedHostPage.RenderBuilderHtml(new EmbedHostViewModel
            {
                FormId = parsedFormId ? formId : null,
                HubBaseUrl = hubBaseUrl,
                HeightMode = heightMode,
                Prefill = prefill,
                Token = token,
                RequestedHubBaseUrl = requestedHub,
                PathBase = pathBase,
                MixedContent = mixedContent,
                HttpPlaygroundHref = EmbedHostPage.LocalHttpPlaygroundUrl(context.Request, ServerAddresses(context))
            }));
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
        var html = EmbedHostPage.RenderBuilderHtml(new EmbedHostViewModel
        {
            FormId = formId,
            HubBaseUrl = hubBaseUrl,
            HeightMode = EmbedHostPage.NormalizeHeightMode(query["heightMode"]),
            Prefill = query["prefill"].ToString().NullIfWhiteSpace(),
            Token = query["token"].ToString().NullIfWhiteSpace(),
            RequestedHubBaseUrl = query["hubBaseUrl"].ToString().NullIfWhiteSpace(),
            PathBase = pathBase,
            Error = error,
            FormIdField = formIdField,
            MixedContent = EmbedHostPage.IsMixedContent(context.Request.IsHttps, hubBaseUrl),
            HttpPlaygroundHref = EmbedHostPage.LocalHttpPlaygroundUrl(context.Request, ServerAddresses(context))
        });
        await WriteHtmlAsync(context, html);
    }

    /// <summary>Addresses this server is actually bound to, or null when unavailable.</summary>
    private static IEnumerable<string>? ServerAddresses(HttpContext context) =>
        context.RequestServices
            .GetService<IServer>()?
            .Features.Get<IServerAddressesFeature>()?
            .Addresses;

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
}
