using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.FormSettings;

namespace Endatix.Api.Endpoints.FormSettings;

/// <summary>A form's settings on the wire: every section, and the version a write names in <c>If-Match</c>.</summary>
[Experimental("ENDATIX0005")]
public sealed class FormSettingsModel
{
    /// <summary>The version to write from; 0 before the first write. The <c>ETag</c> header holds the same value.</summary>
    public long Version { get; init; }

    /// <summary>How the form's submission PDFs look.</summary>
    public PdfSettingsModel Pdf { get; init; } = new();

    /// <summary>The form's own webhooks, which replace its tenant's; null when the tenant's apply.</summary>
    public WebHookSettingsModel? WebHooks { get; init; }

    internal static FormSettingsModel Map(FormSettingsDto settings) => new()
    {
        Version = settings.Version,
        Pdf = PdfSettingsModel.Map(settings.Pdf),
        WebHooks = settings.WebHooks is { } webHooks ? WebHookSettingsModel.Map(webHooks) : null,
    };
}

/// <summary>The PDF section on the wire.</summary>
[Experimental("ENDATIX0005")]
public sealed class PdfSettingsModel
{
    /// <summary>Whether the form's submission PDFs use its theme. Off by default.</summary>
    public bool UsesFormTheme { get; init; }

    internal static PdfSettingsModel Map(PdfSettings pdf) => new() { UsesFormTheme = pdf.UsesFormTheme };

    internal PdfSettings ToSection() => new() { UsesFormTheme = UsesFormTheme };
}

/// <summary>The webhooks section on the wire: the events the form sends, keyed by event name, e.g.
/// <c>SubmissionCompleted</c>.</summary>
[Experimental("ENDATIX0005")]
public sealed class WebHookSettingsModel
{
    /// <summary>Each configured event, keyed by its name.</summary>
    public Dictionary<string, WebHookEventSettingsModel> Events { get; init; } = [];

    // A stored document can hold a JSON null where an object or a list belongs (the form endpoints accept it); it reads
    // as nothing configured there.
    internal static WebHookSettingsModel Map(WebHookConfiguration webHooks) => new()
    {
        Events = (webHooks.Events ?? [])
            .Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => WebHookEventSettingsModel.Map(pair.Value)),
    };

    internal WebHookConfiguration ToSection() => new()
    {
        Events = Events.ToDictionary(pair => pair.Key, pair => pair.Value.ToSection()),
    };
}

/// <summary>One webhook event on the wire.</summary>
[Experimental("ENDATIX0005")]
public sealed class WebHookEventSettingsModel
{
    /// <summary>Whether the event is sent.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>Where the event is sent.</summary>
    public List<WebHookEndpointSettingsModel> WebHookEndpoints { get; init; } = [];

    internal static WebHookEventSettingsModel Map(WebHookEventConfig config) => new()
    {
        IsEnabled = config.IsEnabled,
        WebHookEndpoints = [.. (config.WebHookEndpoints ?? []).OfType<WebHookEndpointConfig>().Select(WebHookEndpointSettingsModel.Map)],
    };

    internal WebHookEventConfig ToSection() => new()
    {
        IsEnabled = IsEnabled,
        WebHookEndpoints = [.. WebHookEndpoints.Select(endpoint => endpoint.ToSection())],
    };
}

/// <summary>One webhook endpoint on the wire.</summary>
[Experimental("ENDATIX0005")]
public sealed class WebHookEndpointSettingsModel
{
    /// <summary>The absolute http or https URL the event is posted to.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>How the request authenticates; none when null.</summary>
    public WebHookAuthSettingsModel? Authentication { get; init; }

    internal static WebHookEndpointSettingsModel Map(WebHookEndpointConfig config) => new()
    {
        Url = config.Url ?? string.Empty,
        Authentication = config.Authentication is { } auth ? WebHookAuthSettingsModel.Map(auth) : null,
    };

    internal WebHookEndpointConfig ToSection() => new() { Url = Url, Authentication = Authentication?.ToSection() };
}

/// <summary>A webhook endpoint's authentication on the wire.</summary>
[Experimental("ENDATIX0005")]
public sealed class WebHookAuthSettingsModel
{
    /// <summary><c>None</c> or <c>ApiKey</c>.</summary>
    public string Type { get; init; } = "None";

    /// <summary>The API key, when <see cref="Type"/> is <c>ApiKey</c>.</summary>
    public string? ApiKey { get; init; }

    /// <summary>The header that carries the API key, when <see cref="Type"/> is <c>ApiKey</c>.</summary>
    public string? ApiKeyHeader { get; init; }

    internal static WebHookAuthSettingsModel Map(WebHookAuthConfig config) =>
        new() { Type = config.Type, ApiKey = config.ApiKey, ApiKeyHeader = config.ApiKeyHeader };

    internal WebHookAuthConfig ToSection() => new() { Type = Type, ApiKey = ApiKey, ApiKeyHeader = ApiKeyHeader };
}
