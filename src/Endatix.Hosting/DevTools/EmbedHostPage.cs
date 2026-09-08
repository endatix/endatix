using System.Globalization;
using System.Net;
using System.Text;

namespace Endatix.Hosting.DevTools;

internal static class EmbedHostPage
{
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

        if (!IsAllowedHost(uri.Host, options))
        {
            return false;
        }

        hubBaseUrl = new Uri($"{uri.Scheme}://{uri.Authority}");
        return true;
    }

    public static string? NormalizeHeightMode(string? heightMode)
    {
        if (string.Equals(heightMode, "fill", StringComparison.Ordinal))
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
        bool bare)
    {
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

        return parts.Count == 0 ? Path : Path + "?" + string.Join("&", parts);
    }

    public static string RenderBuilderHtml(
        string? formId,
        Uri hubBaseUrl,
        string? heightMode,
        string? prefill,
        string? token,
        string? requestedHubBaseUrl)
    {
        var stage = formId is null
            ? EmptyStageHtml
            : $"""<div class="frame{(heightMode is null ? string.Empty : " frame--fill")}" id="endatix-embed-root">{BuildScriptTag(formId, hubBaseUrl, heightMode, prefill, token)}</div>""";

        return EmbedHostAssets.BuilderHtml
            .Replace("__STYLES__", EmbedHostAssets.Styles, StringComparison.Ordinal)
            .Replace("__SCRIPT__", EmbedHostAssets.Script, StringComparison.Ordinal)
            .Replace("__META__", BuildMeta(formId, hubBaseUrl, heightMode, prefill, token), StringComparison.Ordinal)
            .Replace("__ACTIONS__", BuildActions(formId, hubBaseUrl, heightMode, prefill, token, requestedHubBaseUrl), StringComparison.Ordinal)
            .Replace("__STAGE__", stage, StringComparison.Ordinal)
            .Replace("__CONFIG_FORCE_OPEN__", formId is null ? "true" : "false", StringComparison.Ordinal)
            .Replace("__FORM_ID__", Encode(formId ?? string.Empty), StringComparison.Ordinal)
            .Replace("__FILL_SELECTED__", heightMode is not null ? " selected" : string.Empty, StringComparison.Ordinal)
            .Replace("__TOKEN__", Encode(token ?? string.Empty), StringComparison.Ordinal)
            .Replace("__PREFILL__", Encode(prefill ?? string.Empty), StringComparison.Ordinal)
            .Replace("__HUB_FIELD__", Encode(requestedHubBaseUrl ?? string.Empty), StringComparison.Ordinal)
            .Replace("__HUB_PLACEHOLDER__", Encode(hubBaseUrl.GetLeftPart(UriPartial.Authority)), StringComparison.Ordinal);
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
            .Replace("__EMBED_SCRIPT__", BuildScriptTag(formId, hubBaseUrl, heightMode, prefill, token), StringComparison.Ordinal);
    }

    private const string EmptyStageHtml = """
        <div class="empty">
          <h2>No form loaded</h2>
          <p>Set a <code>formId</code> in <b>Configure</b>, then Apply. The query string is the contract &mdash; agents drive this page by URL alone.</p>
        </div>
        """;

    private static string BuildMeta(
        string? formId,
        Uri hubBaseUrl,
        string? heightMode,
        string? prefill,
        string? token)
    {
        var meta = new StringBuilder();
        meta.Append(Pill("formId", formId ?? "not set", warn: formId is null));
        meta.Append(Pill("height", heightMode ?? "auto", warn: false));
        meta.Append(Pill("hub", hubBaseUrl.Authority, warn: false));

        if (!string.IsNullOrEmpty(token))
        {
            meta.Append(Pill("token", "set", warn: false));
        }
        else if (!string.IsNullOrEmpty(prefill))
        {
            meta.Append(Pill("prefill", prefill, warn: false));
        }

        return meta.ToString();
    }

    private static string BuildActions(
        string? formId,
        Uri hubBaseUrl,
        string? heightMode,
        string? prefill,
        string? token,
        string? requestedHubBaseUrl)
    {
        if (formId is null)
        {
            return string.Empty;
        }

        var bareUrl = Encode(ToRelativeUrl(formId, heightMode, prefill, token, requestedHubBaseUrl, bare: true));
        var snippet = Encode(BuildScriptTag(formId, hubBaseUrl, heightMode, prefill, token));

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

    private static string BuildScriptTag(
        string formId,
        Uri hubBaseUrl,
        string? heightMode,
        string? prefill,
        string? token)
    {
        var scriptSrc = $"{hubBaseUrl.GetLeftPart(UriPartial.Authority)}/embed/v1/embed.js";
        var attributes = new StringBuilder();
        attributes.Append(" src=\"").Append(Encode(scriptSrc)).Append('"');
        attributes.Append(" data-form-id=\"").Append(Encode(formId)).Append('"');

        if (heightMode is not null)
        {
            attributes.Append(" data-height-mode=\"").Append(Encode(heightMode)).Append('"');
        }

        if (!string.IsNullOrEmpty(token))
        {
            attributes.Append(" data-token=\"").Append(Encode(token)).Append('"');
        }
        else if (!string.IsNullOrEmpty(prefill))
        {
            attributes.Append(" data-prefill=\"").Append(Encode(prefill)).Append('"');
        }

        return $"<script{attributes}></script>";
    }

    private static bool IsAllowedHost(string host, EmbedHostOptions options)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Uri.TryCreate(options.HubBaseUrl, UriKind.Absolute, out var configured) &&
            string.Equals(host, configured.Host, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var allowed in options.AllowedHubHosts)
        {
            if (string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
