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
/// Storage: https://{host}/{container}/s/{formId}/{submissionId}/{fileName}
/// Hub: {hubBaseUrl}/forms/{formId}/submissions/{submissionId}/files/{fileName}
/// </summary>
public sealed class StorageUrlRewriteTransformer : IValueTransformer
{
    private const string ContentProperty = "content";

    private readonly string _hubUrlBase;
    private readonly string _storageHost;
    private readonly string _storageContainer;
    private readonly ILogger<StorageUrlRewriteTransformer> _logger;
    private readonly bool _enabled;

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

        var host = string.Empty;
        var container = string.Empty;
        if (azureBlobOptions.Value is { IsConfigured: true } azure)
        {
            host = azure.HostName.Trim();
            container = azure.UserFilesContainerName.Trim();
        }

        _storageHost = host;
        _storageContainer = container.ToLowerInvariant();
        _enabled = !string.IsNullOrWhiteSpace(_hubUrlBase) && !string.IsNullOrWhiteSpace(_storageHost);
    }

    /// <inheritdoc />
    public object? Transform<T>(object? value, TransformationContext<T> context)
    {
        if (!_enabled || context.Row is not SubmissionExportRow row)
        {
            return value;
        }

        var formId = row.FormId;
        var submissionId = row.Id;

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Array => ProcessJsonArray(element.GetRawText(), formId, submissionId) ?? value,
                JsonValueKind.Object => ProcessSingleObject(element.GetRawText(), formId, submissionId) ?? value,
                JsonValueKind.String => ProcessStringValue(element.GetString(), formId, submissionId),
                _ => value
            };
        }

        if (value is string s)
        {
            if (s.TrimStart().StartsWith('['))
            {
                return ProcessJsonArray(s, formId, submissionId) ?? value;
            }
            return TryRewriteUrl(s, formId, submissionId) ?? value;
        }

        return value;
    }

    private string ProcessStringValue(string? value, long formId, long submissionId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }
        return value.TrimStart().StartsWith('[')
            ? ProcessJsonArray(value, formId, submissionId) ?? value
            : TryRewriteUrl(value, formId, submissionId) ?? value;
    }

    private string? ProcessJsonArray(string json, long formId, long submissionId)
    {
        if (!json.Contains(_storageHost, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var root = JsonNode.Parse(json);
            if (root is not JsonArray array)
            {
                return null;
            }

            var modified = false;
            foreach (var node in array)
            {
                if (node is JsonObject obj &&
                    obj.TryGetPropertyValue(ContentProperty, out var contentNode) &&
                    contentNode?.GetValue<string>() is string url)
                {
                    var newUrl = TryRewriteUrl(url, formId, submissionId);
                    if (newUrl is not null && newUrl != url)
                    {
                        obj[ContentProperty] = newUrl;
                        modified = true;
                    }
                }
            }

            return modified ? root.ToJsonString() : null;
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse JSON for row {RowId}", submissionId);
            return null;
        }
    }

    private string? ProcessSingleObject(string json, long formId, long submissionId)
    {
        try
        {
            var root = JsonNode.Parse(json);
            if (root is not JsonObject obj ||
                !obj.TryGetPropertyValue(ContentProperty, out var contentNode) ||
                contentNode?.GetValue<string>() is not string url)
            {
                return null;
            }

            var newUrl = TryRewriteUrl(url, formId, submissionId);
            if (newUrl is null || newUrl == url)
            {
                return null;
            }

            obj[ContentProperty] = newUrl;
            return root.ToJsonString();
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse JSON object for row {RowId}", submissionId);
            return null;
        }
    }

    private string? TryRewriteUrl(string url, long formId, long submissionId)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var (isValid, fileName) = IsValidStorageUrl(uri, formId, submissionId);
        if (!isValid)
        {
            return null;
        }

        return $"{_hubUrlBase}/forms/{formId}/submissions/{submissionId}/files/{fileName}";
    }


    /// <summary>
    /// Validates if the URL is a valid storage URL and returns the file name on success.
    /// </summary>
    /// <param name="uri">The URL to validate.</param>
    /// <param name="formId">The form ID.</param>
    /// <param name="submissionId">The submission ID.</param>
    /// <returns>A tuple containing a boolean indicating if the URL is valid and the file name if it is.</returns>
    private (bool IsValid, string? FileName) IsValidStorageUrl(Uri uri, long formId, long submissionId)
    {
        var segments = uri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 5)
        {
            return (false, null);
        }


        if (!string.Equals(uri.Host, _storageHost, StringComparison.OrdinalIgnoreCase))
        {
            return (false, null);
        }

        if (!string.Equals(segments[0], _storageContainer, StringComparison.OrdinalIgnoreCase))
        {
            return (false, null);
        }

        if (!string.Equals(segments[1], "s", StringComparison.Ordinal))
        {
            return (false, null);
        }

        if (!long.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out var urlFormId) || urlFormId != formId)
        {
            return (false, null);
        }

        if (!long.TryParse(segments[3], NumberStyles.None, CultureInfo.InvariantCulture, out var urlSubmissionId) || urlSubmissionId != submissionId)
        {
            return (false, null);
        }

        var fileName = string.Join('/', segments.Skip(4));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return (false, null);
        }

        return (true, fileName);
    }
}
