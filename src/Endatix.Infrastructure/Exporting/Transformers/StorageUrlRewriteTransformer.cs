using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private static readonly byte[] _httpPrefix = "https://"u8.ToArray();
    private static readonly byte[] _httpPrefixHttp = "http://"u8.ToArray();
    private static readonly byte[] _pathPrefixS = "/s/"u8.ToArray();

    private readonly string _hubUrlBase;
    private readonly List<(byte[] HostUtf8, byte[] ContainerUtf8)> _detectionRules;

    public StorageUrlRewriteTransformer(
        IOptions<HubSettings> hubSettings,
        IOptions<AzureBlobStorageProviderOptions> azureBlobOptions,
        ILogger<StorageUrlRewriteTransformer> logger)
    {
        var hubUrl = hubSettings?.Value?.HubBaseUrl?.Trim() ?? string.Empty;
        _hubUrlBase = string.IsNullOrEmpty(hubUrl) ? string.Empty : hubUrl.TrimEnd('/');
        _ = logger ?? throw new ArgumentNullException(nameof(logger));

        _detectionRules = new List<(byte[], byte[])>();
        if (azureBlobOptions?.Value is { IsConfigured: true } azure)
        {
            _detectionRules.Add((
                Encoding.UTF8.GetBytes(azure.HostName.Trim()),
                Encoding.UTF8.GetBytes(azure.UserFilesContainerName.Trim().ToLowerInvariant())
            ));
        }
    }

    /// <inheritdoc />
    public object? Transform<T>(object? value, TransformationContext<T> context)
    {
        if (string.IsNullOrEmpty(_hubUrlBase) || _detectionRules.Count == 0)
        {
            return value;
        }

        long formId;
        long submissionId;
        if (context.Row is SubmissionExportRow row)
        {
            formId = row.FormId;
            submissionId = row.Id;
        }
        else
        {
            return value;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Array => TryRewriteArray(element, formId, submissionId) ?? value,
                JsonValueKind.Object => TryRewriteSingleObject(element, formId, submissionId) ?? value,
                JsonValueKind.String => TryRewriteStringValue(element, formId, submissionId) ?? element.GetString(),
                _ => value
            };
        }

        if (value is string jsonString && jsonString.TrimStart().StartsWith('['))
        {
            return TryRewriteStringifiedArray(jsonString, formId, submissionId) ?? value;
        }

        return value;
    }

    private string? TryRewriteArray(JsonElement element, long formId, long submissionId)
    {
        var node = JsonNode.Parse(element.GetRawText());
        if (node is not JsonArray array)
        {
            return null;
        }
        if (!RewriteUrlsInNode(array, formId, submissionId, out _))
        {
            return null;
        }
        return node.ToJsonString();
    }

    private string? TryRewriteSingleObject(JsonElement element, long formId, long submissionId)
    {
        var node = JsonNode.Parse(element.GetRawText());
        if (node is not JsonObject obj)
        {
            return null;
        }

        if (!obj.TryGetPropertyValue(ContentProperty, out var contentNode) || contentNode?.GetValue<string>() is not string oldUrl)
        {
            return null;
        }

        if (!TryRewriteContentUrl(Encoding.UTF8.GetBytes(oldUrl).AsSpan(), formId, submissionId, out var newUrl))
        {
            return null;
        }

        obj[ContentProperty] = newUrl;
        return node.ToJsonString();
    }

    private string? TryRewriteStringValue(JsonElement element, long formId, long submissionId)
    {
        var value = element.GetString();
        if (string.IsNullOrEmpty(value) || value.Length < 2 || value[0] != '[')
        {
            return null;
        }

        return TryRewriteStringifiedArray(value, formId, submissionId);
    }

    private string? TryRewriteStringifiedArray(string jsonString, long formId, long submissionId)
    {
        ReadOnlySpan<byte> valueUtf8 = Encoding.UTF8.GetBytes(jsonString).AsSpan();
        if (valueUtf8.Length < 2 || valueUtf8[0] != (byte)'[')
        {
            return null;
        }

        var hasStoragePattern = false;
        foreach ((var hostUtf8, _) in _detectionRules)
        {
            if (hostUtf8.Length > 0 && valueUtf8.IndexOf(hostUtf8.AsSpan()) >= 0)
            {
                hasStoragePattern = true;
                break;
            }
        }

        if (!hasStoragePattern)
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

            if (!RewriteUrlsInNode(array, formId, submissionId, out _))
            {
                return null;
            }

            return node.ToJsonString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private bool RewriteUrlsInNode(JsonArray array, long formId, long submissionId, out bool anyRewrite)
    {
        anyRewrite = false;
        foreach (var item in array)
        {
            if (item is not JsonObject obj || !obj.TryGetPropertyValue(ContentProperty, out var contentNode) || contentNode?.GetValue<string>() is not string oldUrl)
            {
                continue;
            }

            if (!TryRewriteContentUrl(Encoding.UTF8.GetBytes(oldUrl).AsSpan(), formId, submissionId, out var newUrl))
            {
                continue;
            }

            obj[ContentProperty] = newUrl;
            anyRewrite = true;
        }
        return anyRewrite;
    }

    private bool TryRewriteContentUrl(ReadOnlySpan<byte> urlUtf8, long formId, long submissionId, out string? hubUrl)
    {
        hubUrl = null;
        if (urlUtf8.Length < 10)
        {
            return false;
        }

        var span = urlUtf8;
        var schemeLen = span.StartsWith(_httpPrefix) ? 8 : (span.StartsWith(_httpPrefixHttp) ? 7 : 0);
        if (schemeLen == 0)
        {
            return false;
        }

        span = span[schemeLen..];

        foreach ((var hostUtf8, var containerUtf8) in _detectionRules)
        {
            if (hostUtf8.Length == 0 || containerUtf8.Length == 0)
            {
                continue;
            }

            var remaining = span;
            if (!remaining.StartsWith(hostUtf8))
            {
                continue;
            }

            remaining = remaining[hostUtf8.Length..];
            if (remaining.Length < 1 || remaining[0] != (byte)'/')
            {
                continue;
            }

            remaining = remaining[1..];
            if (!remaining.StartsWith(containerUtf8))
            {
                continue;
            }

            remaining = remaining[containerUtf8.Length..];
            if (remaining.Length < 4 || !remaining.StartsWith(_pathPrefixS))
            {
                continue;
            }

            remaining = remaining[3..];

            var slash1 = remaining.IndexOf((byte)'/');
            if (slash1 <= 0)
            {
                continue;
            }

            var formIdSpan = remaining[..slash1];
            remaining = remaining[(slash1 + 1)..];
            var slash2 = remaining.IndexOf((byte)'/');
            if (slash2 <= 0)
            {
                continue;
            }
            var submissionIdSpan = remaining[..slash2];
            var fileNameSpan = remaining[(slash2 + 1)..];
            var q = fileNameSpan.IndexOf((byte)'?');
            if (q >= 0)
            {
                fileNameSpan = fileNameSpan[..q];
            }

            if (fileNameSpan.IsEmpty)
            {
                continue;
            }

            if (!TryParseLongUtf8(formIdSpan, out var urlFormId) || urlFormId != formId)
            {
                continue;
            }

            if (!TryParseLongUtf8(submissionIdSpan, out var urlSubmissionId) || urlSubmissionId != submissionId)
            {
                continue;
            }

            var fileName = Encoding.UTF8.GetString(fileNameSpan);
            hubUrl = $"{_hubUrlBase}/forms/{formId}/submissions/{submissionId}/files/{fileName}";
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseLongUtf8(ReadOnlySpan<byte> span, out long value)
    {
        value = 0;
        if (span.IsEmpty)
        {
            return false;
        }
        for (var i = 0; i < span.Length; i++)
        {
            var b = span[i];
            if (b < (byte)'0' || b > (byte)'9')
            {
                return false;
            }
            value = (value * 10) + (b - (byte)'0');
        }
        return true;
    }
}
