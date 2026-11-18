using System.Text.Json;
using Endatix.Core.Common;

namespace Endatix.Core.Entities;

/// <summary>
/// Represents the complete webhook configuration for a tenant or form.
/// This configuration defines which webhook events are enabled and where they should be sent.
/// </summary>
public class WebHookConfiguration
{
    /// <summary>Retired event keys and the key that replaced each.</summary>
    public static IReadOnlyDictionary<string, string> RetiredEventKeys { get; } = new Dictionary<string, string>
    {
        ["SubmissionCollectionStatusChanged"] = "SubmissionClosed",
    };

    /// <summary>
    /// Dictionary of webhook events keyed by event name.
    /// Supported events: FormCreated, FormUpdated, FormEnabledStateChanged, FormDeleted, SubmissionCompleted,
    /// SubmissionClosed
    /// </summary>
    public Dictionary<string, WebHookEventConfig> Events { get; set; } = new();

    /// <summary>
    /// Why a webhook settings document can't be stored, or null. A document that still uses a retired event key is
    /// refused, so an older client fails loudly instead of losing a subscription. Malformed JSON is not judged here.
    /// </summary>
    public static string? RetiredKeyError(string? webHookSettingsJson)
    {
        if (string.IsNullOrWhiteSpace(webHookSettingsJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(webHookSettingsJson);
            return RetiredKeyError(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc cref="RetiredKeyError(string?)"/>
    public static string? RetiredKeyError(JsonElement webHookSettings)
    {
        if (webHookSettings.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var events = webHookSettings.EnumerateObject()
            .FirstOrDefault(property => string.Equals(property.Name, nameof(Events), StringComparison.OrdinalIgnoreCase));
        if (events.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var retired = events.Value.EnumerateObject()
            .Select(property => property.Name)
            .FirstOrDefault(key => RetiredEventKeys.Keys.Contains(key, StringComparer.OrdinalIgnoreCase));
        return retired is null ? null : RetiredKeyMessage(retired);
    }

    /// <summary>Why a configuration can't be stored because it still uses a retired event key, or null.</summary>
    public static string? RetiredKeyError(WebHookConfiguration? settings) =>
        settings?.Events.Keys.FirstOrDefault(key => RetiredEventKeys.Keys.Contains(key, StringComparer.OrdinalIgnoreCase))
            is { } retired
            ? RetiredKeyMessage(retired)
            : null;

    /// <summary>
    /// Why a webhook settings document can't be stored because of an endpoint URL, or null. Every <c>url</c> property in
    /// the document is judged, in any casing, so it covers whatever a reader of the document takes as the endpoint. A URL
    /// must be an absolute http or https URL; unless <paramref name="policy"/> allows private network targets, its host
    /// must not be a private IP literal or a reserved name (<see cref="PublicNetworkAddress.IsKnownPrivateHost"/>, no
    /// DNS). User info is allowed. Blank URLs and malformed JSON are not judged here. The message names no URL.
    /// </summary>
    public static string? EndpointUrlError(string? webHookSettingsJson, OutboundAddressPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(webHookSettingsJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(webHookSettingsJson);
            return EndpointUrlError(document.RootElement, policy);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc cref="EndpointUrlError(string?, OutboundAddressPolicy)"/>
    public static string? EndpointUrlError(JsonElement webHookSettings, OutboundAddressPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var urls = EndpointUrls(webHookSettings).Where(url => !string.IsNullOrWhiteSpace(url)).ToList();
        if (urls.Any(url => !IsAbsoluteHttpUrl(url)))
        {
            return UnsupportedEndpointMessage;
        }

        return !policy.AllowPrivateNetworkTargets && urls.Any(IsPrivateUrl) ? PrivateEndpointMessage : null;
    }

    private const string UnsupportedEndpointMessage = "A webhook endpoint URL must be an absolute http or https URL.";

    private const string PrivateEndpointMessage =
        "A webhook endpoint URL points to a private or local network address. Use a public address.";

    private static bool IsAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.IdnHost);

    private static bool IsPrivateUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && PublicNetworkAddress.IsKnownPrivateHost(uri.IdnHost);

    private static IEnumerable<string> EndpointUrls(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().SelectMany(property =>
            string.Equals(property.Name, nameof(WebHookEndpointConfig.Url), StringComparison.OrdinalIgnoreCase)
            && property.Value.ValueKind == JsonValueKind.String
                ? [property.Value.GetString()!]
                : EndpointUrls(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(EndpointUrls),
        _ => [],
    };

    private static string RetiredKeyMessage(string retiredKey) =>
        $"The webhook setting '{retiredKey}' was renamed to "
        + $"'{RetiredEventKeys.First(pair => string.Equals(pair.Key, retiredKey, StringComparison.OrdinalIgnoreCase)).Value}'. "
        + "Use an Endatix Hub release that writes 'SubmissionClosed'.";
}

/// <summary>
/// Configuration for a specific webhook event.
/// </summary>
public class WebHookEventConfig
{
    /// <summary>
    /// Indicates whether this webhook event is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// List of endpoints where webhook payloads should be sent for this event.
    /// </summary>
    public List<WebHookEndpointConfig> WebHookEndpoints { get; set; } = new();
}

/// <summary>
/// Configuration for a webhook endpoint destination.
/// </summary>
public class WebHookEndpointConfig
{
    /// <summary>
    /// The URL where the webhook payload should be sent.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Optional authentication configuration for the webhook endpoint.
    /// </summary>
    public WebHookAuthConfig? Authentication { get; set; }
}

/// <summary>
/// Authentication configuration for webhook endpoints.
/// </summary>
public class WebHookAuthConfig
{
    /// <summary>
    /// The type of authentication. Supported values: "None", "ApiKey"
    /// </summary>
    public string Type { get; set; } = "None";

    /// <summary>
    /// The API key value (required when Type is "ApiKey").
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The header name for the API key (required when Type is "ApiKey").
    /// </summary>
    public string? ApiKeyHeader { get; set; }
}
