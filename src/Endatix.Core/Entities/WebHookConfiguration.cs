using System.Text.Json;

namespace Endatix.Core.Entities;

/// <summary>
/// Represents the complete webhook configuration for a tenant or form.
/// This configuration defines which webhook events are enabled and where they should be sent.
/// </summary>
public class WebHookConfiguration
{
    /// <summary>The Endatix Hub release that writes <c>SubmissionClosed</c> instead of the retired key.</summary>
    public const string PAIRED_HUB_VERSION = "0.9.1";

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

    /// <summary>Throws when a configuration still uses a retired event key.</summary>
    /// <exception cref="ArgumentException">A retired key is present.</exception>
    public static void EnsureNoRetiredKeys(WebHookConfiguration? settings)
    {
        var retired = settings?.Events.Keys.FirstOrDefault(key => RetiredEventKeys.Keys.Contains(key, StringComparer.OrdinalIgnoreCase));
        if (retired is not null)
        {
            throw new ArgumentException(RetiredKeyMessage(retired), nameof(settings));
        }
    }

    private static string RetiredKeyMessage(string retiredKey) =>
        $"The webhook setting '{retiredKey}' was renamed to "
        + $"'{RetiredEventKeys.First(pair => string.Equals(pair.Key, retiredKey, StringComparison.OrdinalIgnoreCase)).Value}'. "
        + $"Use Endatix Hub {PAIRED_HUB_VERSION} or later, which writes the new setting.";
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
