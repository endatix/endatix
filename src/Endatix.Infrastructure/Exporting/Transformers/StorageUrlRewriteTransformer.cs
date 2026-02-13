using System.Globalization;
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
    private const string CONTENT_PROPERTY_NAME = "content";

    private readonly string _hubUrlBase;
    private readonly string _storageHost;
    private readonly string _storageContainer;
    private readonly bool _shouldTransform;

    public StorageUrlRewriteTransformer(
        IOptions<HubSettings> hubSettings,
        IOptions<AzureBlobStorageProviderOptions> azureBlobOptions,
        ILogger<StorageUrlRewriteTransformer> logger)
    {
        Guard.Against.Null(logger);
        Guard.Against.Null(hubSettings);
        Guard.Against.Null(azureBlobOptions);

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
        _shouldTransform = !string.IsNullOrWhiteSpace(_hubUrlBase) && !string.IsNullOrWhiteSpace(_storageHost);
    }

    public JsonNode? Transform<T>(JsonNode? node, TransformationContext<T> context)
    {
        if (!_shouldTransform)
        {
            return node;
        }

        if (node is null || context.Row is not SubmissionExportRow row)
        {
            return node;
        }

        switch (node)
        {
            case JsonArray array:
                ProcessJsonArray(array, row);
                break;
            case JsonObject obj:
                ProcessJsonObject(obj, row);
                break;
            case JsonValue jsonValue:
                return TransformJsonValue(jsonValue, row);
        }

        return node;
    }

    private void ProcessJsonArray(JsonArray jsonArray, SubmissionExportRow row)
    {
        for (var i = 0; i < jsonArray.Count; i++)
        {
            switch (jsonArray[i])
            {
                case JsonArray subArray:
                    ProcessJsonArray(subArray, row);
                    break;
                case JsonObject obj:
                    ProcessJsonObject(obj, row);
                    break;
                case JsonValue val:
                    val.TryGetValue<string>(out var _);
                    break;
            }
        }
    }

    private void ProcessJsonObject(JsonObject jsonObject, SubmissionExportRow row)
    {
        if (!jsonObject.TryGetPropertyValue(CONTENT_PROPERTY_NAME, out var contentNode))
        {
            return;
        }

        if (contentNode is not JsonValue jsonValue)
        {
            return;
        }

        if (!jsonValue.TryGetValue<string>(out var urlToRewrite))
        {
            return;
        }

        if (TryRewriteUrl(urlToRewrite, row.FormId, row.Id, out var newUrl))
        {
            jsonObject[CONTENT_PROPERTY_NAME] = newUrl;
        }
    }

    private JsonNode? TransformJsonValue(JsonValue jsonValue, SubmissionExportRow row)
    {
        if (!jsonValue.TryGetValue<string>(out var urlToRewrite))
        {
            return jsonValue;
        }

        if (TryRewriteUrl(urlToRewrite, row.FormId, row.Id, out var newUrl))
        {
            return JsonValue.Create(newUrl);
        }

        return jsonValue;
    }

    private bool TryRewriteUrl(string url, long formId, long submissionId, out string rewrittenUrl)
    {
        rewrittenUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var (isValid, fileName) = IsValidStorageUrl(uri, formId, submissionId);
        if (!isValid)
        {
            return false;
        }

        rewrittenUrl = $"{_hubUrlBase}/forms/{formId}/submissions/{submissionId}/files/{fileName}";
        return true;
    }

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

        var fileName = string.Join('/', segments[4..]);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return (false, null);
        }

        return (true, fileName);
    }
}
