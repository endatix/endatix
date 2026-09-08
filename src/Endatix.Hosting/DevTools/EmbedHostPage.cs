using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Endatix.Hosting.DevTools;

internal sealed record EmbedHostViewModel
{
    public required Uri HubBaseUrl { get; init; }
    public string? FormId { get; init; }
    public string? HeightMode { get; init; }
    public string? Prefill { get; init; }
    public string? Token { get; init; }
    public string? RequestedHubBaseUrl { get; init; }
    public string PathBase { get; init; } = "";
    public string? Error { get; init; }
    public string? FormIdField { get; init; }
    public bool MixedContent { get; init; }
    public string? HttpPlaygroundHref { get; init; }
}

internal static class EmbedHostPage
{
    internal static Uri FallbackHubOrigin { get; } =
        new UriBuilder(Uri.UriSchemeHttp, "localhost", 3000).Uri;

    public const string Path = "/dev/embed-host";

    public static bool IsBareView(string? view) =>
        string.Equals(view, "bare", StringComparison.OrdinalIgnoreCase);

    public static bool TryParseFormId(string? formId, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(formId))
        {
            return false;
        }

        var trimmed = formId.Trim();
        if (!long.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ||
            id <= 0)
        {
            return false;
        }

        normalized = id.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    public static bool TryResolveHubBaseUrl(
        string? requestedHubBaseUrl,
        EmbedHostOptions options,
        out Uri hubBaseUrl)
    {
        hubBaseUrl = null!;
        var candidate = string.IsNullOrWhiteSpace(requestedHubBaseUrl)
            ? options.HubBaseUrl
            : requestedHubBaseUrl;

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        if (!IsAllowed(uri, options))
        {
            return false;
        }

        hubBaseUrl = new Uri($"{uri.Scheme}://{uri.Authority}");
        return true;
    }

    public static string? NormalizeHeightMode(string? heightMode)
    {
        if (string.Equals(heightMode, "fill", StringComparison.OrdinalIgnoreCase))
        {
            return "fill";
        }

        return null;
    }

    public static string ToRelativeUrl(
        string? formId,
        string? heightMode,
        string? prefill,
        string? token,
        string? requestedHubBaseUrl,
        bool bare,
        string pathBase = "")
    {
        var path = string.IsNullOrEmpty(pathBase)
            ? Path
            : pathBase.TrimEnd('/') + Path;
        var parts = new List<string>(8);
        if (!string.IsNullOrEmpty(formId))
        {
            parts.Add("formId=" + Uri.EscapeDataString(formId));
        }

        if (heightMode is not null)
        {
            parts.Add("heightMode=" + Uri.EscapeDataString(heightMode));
        }

        if (!string.IsNullOrEmpty(token))
        {
            parts.Add("token=" + Uri.EscapeDataString(token));
        }

        if (!string.IsNullOrEmpty(prefill))
        {
            parts.Add("prefill=" + Uri.EscapeDataString(prefill));
        }

        if (!string.IsNullOrEmpty(requestedHubBaseUrl))
        {
            parts.Add("hubBaseUrl=" + Uri.EscapeDataString(requestedHubBaseUrl));
        }

        if (bare)
        {
            parts.Add("view=bare");
        }

        return parts.Count == 0 ? path : path + "?" + string.Join("&", parts);
    }

    public static bool IsMixedContent(bool pageIsHttps, Uri hubBaseUrl) =>
        pageIsHttps && hubBaseUrl.Scheme == Uri.UriSchemeHttp;

    public static string RenderBuilderHtml(EmbedHostViewModel view)
    {
        string stage;
        if (view.FormId is not null && !view.MixedContent)
        {
            stage = $"""<div class="{FrameClass(view.HeightMode)}" id="endatix-embed-root">{BuildScriptTag(view)}</div>""";
        }
        else if (view.MixedContent)
        {
            stage = MixedContentCard(view.HttpPlaygroundHref);
        }
        else
        {
            stage = EmptyStageHtml;
        }

        var errorHtml = string.IsNullOrEmpty(view.Error)
            ? string.Empty
            : $"<p class=\"banner\" role=\"alert\">{Encode(view.Error)}</p>";

        var forceOpen = view.FormId is null || view.Error is not null || view.MixedContent;

        return EmbedHostAssets.BuilderHtml
            .Replace("__STYLES__", EmbedHostAssets.Styles, StringComparison.Ordinal)
            .Replace("__SCRIPT__", EmbedHostAssets.Script, StringComparison.Ordinal)
            .Replace("__META__", BuildMeta(view), StringComparison.Ordinal)
            .Replace("__ACTIONS__", BuildActions(view), StringComparison.Ordinal)
            .Replace("__ERROR__", errorHtml, StringComparison.Ordinal)
            .Replace("__STAGE__", stage, StringComparison.Ordinal)
            .Replace("__CONFIG_FORCE_OPEN__", forceOpen ? "true" : "false", StringComparison.Ordinal)
            .Replace("__FORM_ID__", Encode(view.FormIdField ?? view.FormId ?? string.Empty), StringComparison.Ordinal)
            .Replace("__FILL_SELECTED__", view.HeightMode is not null ? " selected" : string.Empty, StringComparison.Ordinal)
            .Replace("__TOKEN__", Encode(view.Token ?? string.Empty), StringComparison.Ordinal)
            .Replace("__PREFILL__", Encode(view.Prefill ?? string.Empty), StringComparison.Ordinal)
            .Replace("__HUB_FIELD__", Encode(view.RequestedHubBaseUrl ?? string.Empty), StringComparison.Ordinal)
            .Replace("__HUB_PLACEHOLDER__", Encode(view.HubBaseUrl.GetLeftPart(UriPartial.Authority)), StringComparison.Ordinal);
    }

    public static string? LocalHttpPlaygroundUrl(HttpRequest request)
    {
        if (!request.IsHttps)
        {
            return null;
        }

        var host = request.Host.Host;
        if (!string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = (request.PathBase.HasValue ? request.PathBase.Value : string.Empty) + request.Path.Value;
        var httpPlayground = new UriBuilder(Uri.UriSchemeHttp, "localhost", 5000)
        {
            Path = string.IsNullOrEmpty(path) ? "/" : path,
            Query = request.QueryString.ToString().TrimStart('?')
        };
        return httpPlayground.Uri.ToString();
    }

    public static string RenderBareHtml(
        string formId,
        Uri hubBaseUrl,
        string? heightMode,
        string? prefill,
        string? token)
    {
        var fillCss = heightMode is not null
            ? "html, body, #endatix-embed-root { height: 100%; } #endatix-embed-root { min-height: 100vh; }"
            : string.Empty;

        return EmbedHostAssets.BareHtml
            .Replace("__FILL_CSS__", fillCss, StringComparison.Ordinal)
            .Replace(
                "__EMBED_SCRIPT__",
                BuildScriptTag(new EmbedHostViewModel
                {
                    FormId = formId,
                    HubBaseUrl = hubBaseUrl,
                    HeightMode = heightMode,
                    Prefill = prefill,
                    Token = token
                }),
                StringComparison.Ordinal);
    }

    private static string MixedContentCard(string? httpPlaygroundHref)
    {
        var httpLink = string.IsNullOrEmpty(httpPlaygroundHref)
            ? "<code>http://localhost:5000/dev/embed-host</code> (same query)"
            : $"""<a href="{Encode(httpPlaygroundHref)}">{Encode(httpPlaygroundHref)}</a>""";

        var action = string.IsNullOrEmpty(httpPlaygroundHref)
            ? string.Empty
            : $"""<div class="empty-actions"><a class="btn btn--primary" href="{Encode(httpPlaygroundHref)}">Open HTTP playground</a></div>""";

        return $"""
            <div class="empty empty--error" role="alert">
              <h2>Hub script blocked (mixed content)</h2>
              <p>This page is <b>HTTPS</b>. Hub serves <code>embed.js</code> over <b>HTTP</b>. Browsers refuse that.</p>
              <ol>
                <li>Local default: open the HTTP playground {httpLink}.</li>
                <li>Or set <code>hubBaseUrl</code> to an HTTPS Hub origin in Configure, then Apply.</li>
              </ol>
              {action}
            </div>
            """;
    }


    private const string EmptyStageHtml = """
        <div class="empty">
          <h2>No form loaded</h2>
          <p>Set a <code>formId</code> in <b>Configure</b>, then Apply. The query string is the contract &mdash; agents drive this page by URL alone.</p>
        </div>
        """;

    private static string BuildMeta(EmbedHostViewModel view)
    {
        var meta = new StringBuilder();
        meta.Append(Pill("formId", view.FormId ?? "not set", warn: view.FormId is null));
        meta.Append(Pill("height", view.HeightMode ?? "auto", warn: false));
        meta.Append(Pill("hub", view.HubBaseUrl.Authority, warn: false));

        if (!string.IsNullOrEmpty(view.Token))
        {
            meta.Append(Pill("token", "set", warn: false));
        }
        else if (!string.IsNullOrEmpty(view.Prefill))
        {
            meta.Append(Pill("prefill", view.Prefill, warn: false));
        }

        return meta.ToString();
    }

    private static string BuildActions(EmbedHostViewModel view)
    {
        if (view.FormId is null || view.MixedContent)
        {
            return string.Empty;
        }

        var bareUrl = Encode(ToRelativeUrl(
            view.FormId,
            view.HeightMode,
            view.Prefill,
            view.Token,
            view.RequestedHubBaseUrl,
            bare: true,
            view.PathBase));
        var snippet = Encode(BuildScriptTag(view));

        return $"""
            <div class="seg" role="group" aria-label="Preview width">
                    <button class="seg-btn" type="button" data-width="full" aria-pressed="true">Full</button>
                    <button class="seg-btn" type="button" data-width="768">768</button>
                    <button class="seg-btn" type="button" data-width="375">375</button>
                  </div>
                  <button class="btn" type="button" id="reload-embed" title="Reload the page and re-run embed.js">
                    <svg class="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><polyline points="23 4 23 10 17 10"></polyline><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"></path></svg><span>Reload</span>
                  </button>
                  <button class="btn" type="button" id="copy-snippet" data-snippet="{snippet}" title="Copy the script tag this page renders">
                    <svg class="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="9" y="9" width="13" height="13" rx="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg><span class="btn-label">Copy snippet</span>
                  </button>
                  <a class="btn" target="_blank" rel="noopener noreferrer" href="{bareUrl}" title="Open the embed with no DevTools chrome in a new tab">
                    <span>Open in new tab</span><svg class="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path><polyline points="15 3 21 3 21 9"></polyline><line x1="10" y1="14" x2="21" y2="3"></line></svg>
                  </a>
            """;
    }

    private static string Pill(string label, string value, bool warn) =>
        $"<span class=\"pill{(warn ? " pill--warn" : string.Empty)}\" title=\"{Encode(value)}\"><b>{Encode(label)}</b>{Encode(value)}</span>";

    private static string BuildScriptTag(EmbedHostViewModel view)
    {
        var formId = view.FormId ?? string.Empty;
        var scriptSrc = $"{view.HubBaseUrl.GetLeftPart(UriPartial.Authority)}/embed/v1/embed.js";
        var attributes = new StringBuilder();
        attributes.Append(" src=\"").Append(Encode(scriptSrc)).Append('"');
        attributes.Append(" data-form-id=\"").Append(Encode(formId)).Append('"');

        if (view.HeightMode is not null)
        {
            attributes.Append(" data-height-mode=\"").Append(Encode(view.HeightMode)).Append('"');
        }

        if (!string.IsNullOrEmpty(view.Token))
        {
            attributes.Append(" data-token=\"").Append(Encode(view.Token)).Append('"');
        }
        else if (!string.IsNullOrEmpty(view.Prefill))
        {
            attributes.Append(" data-prefill=\"").Append(Encode(view.Prefill)).Append('"');
        }

        return $"<script{attributes}></script>";
    }

    private static string FrameClass(string? heightMode) =>
        heightMode is null ? "frame" : "frame frame--fill";

    private static bool IsAllowed(Uri uri, EmbedHostOptions options)
    {
        if (Uri.TryCreate(options.HubBaseUrl, UriKind.Absolute, out var configured) &&
            SameOrigin(uri, configured))
        {
            return true;
        }

        if (options.AllowsLoopback && uri.IsLoopback)
        {
            return true;
        }

        foreach (var allowed in options.AllowedHubHosts)
        {
            if (Uri.TryCreate(allowed, UriKind.Absolute, out var origin) && SameOrigin(uri, origin))
            {
                return true;
            }

            if (options.AllowsLoopback &&
                string.Equals(uri.Host, allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SameOrigin(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) &&
        left.Port == right.Port;

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
