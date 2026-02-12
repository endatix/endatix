using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Configuration;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Exporting.Transformers;

/// <summary>
/// Rewrites storage file URLs in JSON values to hub file-details URLs.
/// Implements IValueTransformer; handles JsonElement (Array/Object/String) and string (stringified JSON).
/// </summary>
public sealed class StorageUrlRewriteTransformer : IValueTransformer
{
    private const string ContentProperty = "content";
    private readonly string _hubUrlBase;
    private readonly IReadOnlyList<DetectionRule> _detectionRules;
    private readonly ILogger<StorageUrlRewriteTransformer> _logger;

    public StorageUrlRewriteTransformer(
        IOptions<HubSettings> hubSettings,
        IOptions<AzureBlobStorageProviderOptions> azureBlobOptions,
        ILogger<StorageUrlRewriteTransformer> logger)
    {
        Guard.Against.Null(logger);
        Guard.Against.Null(hubSettings);
        Guard.Against.Null(azureBlobOptions);

        _logger = logger;

        var hubUrl = hubSettings.Value.HubBaseUrl?.Trim() ?? string.Empty;
        _hubUrlBase = string.IsNullOrWhiteSpace(hubUrl) ? string.Empty : hubUrl.TrimEnd('/');

        var rules = new List<DetectionRule>();
        if (azureBlobOptions?.Value is { IsConfigured: true } azure)
        {
            var host = azure.HostName.Trim();
            var container = azure.UserFilesContainerName.Trim();

            if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(container))
            {
                rules.Add(new DetectionRule(host, container));
            }
        }

        _detectionRules = rules;
    }

    /// <inheritdoc />
    public object? Transform<T>(object? value, TransformationContext<T> context)
    {
        if (string.IsNullOrEmpty(_hubUrlBase) || _detectionRules.Count == 0)
        {
            return value;
        }

        if (context.Row is not SubmissionExportRow row)
        {
            return value;
        }

        var formId = row.FormId;
        var submissionId = row.Id;

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Array => TryRewriteArray(element, formId, submissionId) ?? value,
                JsonValueKind.Object => TryRewriteSingleObject(element, formId, submissionId) ?? value,
                JsonValueKind.String => TryRewriteStringValue(element.GetString(), formId, submissionId) ?? element.GetString(),
                _ => value
            };
        }

        if (value is string jsonString)
        {
            var trimmed = jsonString.AsSpan().TrimStart();
            if (!trimmed.IsEmpty && trimmed[0] == '[')
            {
                return TryRewriteStringifiedArray(jsonString, formId, submissionId) ?? value;
            }
        }

        return value;
    }

    private string? TryRewriteArray(JsonElement element, long formId, long submissionId)
    {
        try
        {
            var node = JsonNode.Parse(element.GetRawText());
            if (node is not JsonArray array)
            {
                return null;
            }

            if (!RewriteUrlsInNode(array, formId, submissionId, out var anyRewrite) || !anyRewrite)
            {
                return null;
            }

            return node.ToJsonString();
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse JsonElement array for row {RowId}. Returning original value.", submissionId);
            return null;
        }
    }

    private string? TryRewriteSingleObject(JsonElement element, long formId, long submissionId)
    {
        try
        {
            var node = JsonNode.Parse(element.GetRawText());
            if (node is not JsonObject obj)
            {
                return null;
            }

            if (!obj.TryGetPropertyValue(ContentProperty, out var contentNode) ||
                contentNode?.GetValue<string>() is not string oldUrl)
            {
                return null;
            }

            if (!TryRewriteContentUrl(oldUrl, formId, submissionId, out var newUrl))
            {
                return null;
            }

            obj[ContentProperty] = newUrl;
            return node.ToJsonString();
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse JsonElement object for row {RowId}. Returning original value.", submissionId);
            return null;
        }
    }

    private string? TryRewriteStringValue(string? value, long formId, long submissionId)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 2 || value[0] != '[')
        {
            return null;
        }

        return TryRewriteStringifiedArray(value, formId, submissionId);
    }

    private string? TryRewriteStringifiedArray(string jsonString, long formId, long submissionId)
    {
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return null;
        }

        var trimmed = jsonString.AsSpan().TrimStart();
        if (trimmed.IsEmpty || trimmed[0] != '[')
        {
            return null;
        }

        if (!_detectionRules.Any(r => jsonString.Contains(r.Host, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(jsonString);
            if (node is not JsonArray array)
            {
                return null;
            }

            if (!RewriteUrlsInNode(array, formId, submissionId, out var anyRewrite) || !anyRewrite)
            {
                return null;
            }

            return node.ToJsonString();
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse JSON stringified array for row {RowId}. Returning original value.", submissionId);
            return null;
        }
    }

    private bool RewriteUrlsInNode(JsonArray array, long formId, long submissionId, out bool anyRewrite)
    {
        anyRewrite = false;
        foreach (var item in array)
        {
            if (item is not JsonObject obj ||
                !obj.TryGetPropertyValue(ContentProperty, out var contentNode) ||
                contentNode?.GetValue<string>() is not string oldUrl)
            {
                continue;
            }

            if (!TryRewriteContentUrl(oldUrl, formId, submissionId, out var newUrl))
            {
                continue;
            }

            obj[ContentProperty] = newUrl;
            anyRewrite = true;
        }
        return anyRewrite;
    }

    private bool TryRewriteContentUrl(string url, long formId, long submissionId, out string? hubUrl)
    {
        hubUrl = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var rule in _detectionRules)
        {
            if (!string.Equals(uri.Host, rule.Host, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 5)
            {
                continue;
            }

            if (!string.Equals(segments[0], rule.Container, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(segments[1], "s", StringComparison.Ordinal))
            {
                continue;
            }

            if (!long.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out var urlFormId) ||
                urlFormId != formId)
            {
                continue;
            }

            if (!long.TryParse(segments[3], NumberStyles.None, CultureInfo.InvariantCulture, out var urlSubmissionId) ||
                urlSubmissionId != submissionId)
            {
                continue;
            }

            var fileName = string.Join('/', segments.Skip(4));
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            hubUrl = $"{_hubUrlBase}/forms/{formId}/submissions/{submissionId}/files/{fileName}";
            return true;
        }

        return false;
    }

    private sealed record DetectionRule(string Host, string Container);
}
