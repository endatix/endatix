using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Configuration;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Exporting.Transformers;

/// <summary>
/// Rewrites storage file URLs in JSON values to hub file-details URLs.
/// Implements IJsonValueTransformer for the column-by-column export pipeline.
/// </summary>
public sealed class StorageUrlRewriteTransformer : IJsonValueTransformer<SubmissionExportRow>
{
    private static readonly byte[] _httpPrefix = "https://"u8.ToArray();
    private static readonly byte[] _httpPrefixHttp = "http://"u8.ToArray();
    private static readonly byte[] _pathPrefixS = "/s/"u8.ToArray();

    private readonly string _hubUrlBase;
    private readonly List<(byte[] HostUtf8, byte[] ContainerUtf8)> _detectionRules;
    private readonly ILogger<StorageUrlRewriteTransformer> _logger;

    public StorageUrlRewriteTransformer(
        IOptions<HubSettings> hubSettings,
        IOptions<AzureBlobStorageProviderOptions> azureBlobOptions,
        ILogger<StorageUrlRewriteTransformer> logger)
    {
        string hubUrl = hubSettings?.Value?.HubBaseUrl?.Trim() ?? string.Empty;
        _hubUrlBase = string.IsNullOrEmpty(hubUrl) ? string.Empty : hubUrl.TrimEnd('/');
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
    public object? Transform(JsonElement element, SubmissionExportRow row)
    {
        if (string.IsNullOrEmpty(_hubUrlBase) || _detectionRules.Count == 0)
            return null;

        long formId = row.FormId;
        long submissionId = row.Id;

        return element.ValueKind switch
        {
            JsonValueKind.Array => TryRewriteArray(element, formId, submissionId),
            JsonValueKind.Object => TryRewriteSingleObject(element, formId, submissionId),
            JsonValueKind.String => TryRewriteStringValue(element, formId, submissionId) ?? element.GetString(),
            _ => null
        };
    }

    private string? TryRewriteArray(JsonElement element, long formId, long submissionId)
    {
        bool anyRewrite = false;
        var buffer = new ArrayBufferWriter<byte>(initialCapacity: 256);
        using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false, SkipValidation = true }))
        {
            w.WriteStartArray();
            foreach (JsonElement el in element.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                {
                    el.WriteTo(w);
                    continue;
                }
                w.WriteStartObject();
                foreach (JsonProperty prop in el.EnumerateObject())
                {
                    if (prop.NameEquals("content") && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        string? content = prop.Value.GetString();
                        if (content is not null)
                        {
                            ReadOnlySpan<byte> contentUtf8 = Encoding.UTF8.GetBytes(content).AsSpan();
                            if (TryRewriteContentUrl(contentUtf8, formId, submissionId, out string? newUrl))
                            {
                                w.WriteString("content", newUrl);
                                anyRewrite = true;
                            }
                            else
                                prop.WriteTo(w);
                        }
                        else
                            prop.WriteTo(w);
                    }
                    else
                        prop.WriteTo(w);
                }
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.Flush();
        }
        return anyRewrite ? Encoding.UTF8.GetString(buffer.WrittenSpan) : null;
    }

    private string? TryRewriteSingleObject(JsonElement element, long formId, long submissionId)
    {
        if (!element.TryGetProperty("content", out JsonElement contentProp) || contentProp.ValueKind != JsonValueKind.String)
            return null;
        string? content = contentProp.GetString();
        if (content is null)
            return null;
        ReadOnlySpan<byte> contentUtf8 = Encoding.UTF8.GetBytes(content).AsSpan();
        if (!TryRewriteContentUrl(contentUtf8, formId, submissionId, out string? newUrl))
            return null;
        var buffer = new ArrayBufferWriter<byte>(initialCapacity: 256);
        using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false, SkipValidation = true }))
        {
            w.WriteStartObject();
            foreach (JsonProperty prop in element.EnumerateObject())
            {
                if (prop.NameEquals("content"))
                    w.WriteString("content", newUrl);
                else
                    prop.WriteTo(w);
            }
            w.WriteEndObject();
            w.Flush();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private string? TryRewriteStringValue(JsonElement element, long formId, long submissionId)
    {
        string? value = element.GetString();
        if (string.IsNullOrEmpty(value) || value.Length < 2 || value[0] != '[')
            return null;
        ReadOnlySpan<byte> valueUtf8 = Encoding.UTF8.GetBytes(value).AsSpan();
        return TryRewriteStringifiedFileArray(valueUtf8, formId, submissionId, out string? rewritten) ? rewritten : null;
    }

    private bool TryRewriteStringifiedFileArray(ReadOnlySpan<byte> valueUtf8, long formId, long submissionId, out string? rewritten)
    {
        rewritten = null;
        if (valueUtf8.Length < 2 || valueUtf8[0] != (byte)'[')
            return false;
        bool hasStoragePattern = false;
        foreach ((byte[] hostUtf8, _) in _detectionRules)
        {
            if (hostUtf8.Length > 0 && valueUtf8.IndexOf(hostUtf8.AsSpan()) >= 0)
            {
                hasStoragePattern = true;
                break;
            }
        }
        if (!hasStoragePattern)
            return false;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(valueUtf8.ToArray());
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return false;
            var buffer = new ArrayBufferWriter<byte>(initialCapacity: valueUtf8.Length);
            using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false, SkipValidation = true }))
            {
                w.WriteStartArray();
                foreach (JsonElement el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object)
                    {
                        el.WriteTo(w);
                        continue;
                    }
                    w.WriteStartObject();
                    foreach (JsonProperty prop in el.EnumerateObject())
                    {
                        if (prop.NameEquals("content") && prop.Value.ValueKind == JsonValueKind.String)
                        {
                            string? content = prop.Value.GetString();
                            if (content is not null)
                            {
                                ReadOnlySpan<byte> contentUtf8 = Encoding.UTF8.GetBytes(content).AsSpan();
                                if (TryRewriteContentUrl(contentUtf8, formId, submissionId, out string? newUrl))
                                    w.WriteString("content", newUrl);
                                else
                                    prop.WriteTo(w);
                            }
                            else
                                prop.WriteTo(w);
                        }
                        else
                            prop.WriteTo(w);
                    }
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.Flush();
            }
            rewritten = Encoding.UTF8.GetString(buffer.WrittenSpan);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private bool TryRewriteContentUrl(ReadOnlySpan<byte> urlUtf8, long formId, long submissionId, out string? hubUrl)
    {
        hubUrl = null;
        if (urlUtf8.Length < 10)
            return false;

        ReadOnlySpan<byte> span = urlUtf8;
        int schemeLen = span.StartsWith(_httpPrefix) ? 8 : (span.StartsWith(_httpPrefixHttp) ? 7 : 0);
        if (schemeLen == 0)
            return false;
        span = span.Slice(schemeLen);

        foreach ((byte[] hostUtf8, byte[] containerUtf8) in _detectionRules)
        {
            if (hostUtf8.Length == 0 || containerUtf8.Length == 0)
                continue;
            ReadOnlySpan<byte> remaining = span;
            if (!remaining.StartsWith(hostUtf8))
                continue;
            remaining = remaining.Slice(hostUtf8.Length);
            if (remaining.Length < 1 || remaining[0] != (byte)'/')
                continue;
            remaining = remaining.Slice(1);
            if (!remaining.StartsWith(containerUtf8))
                continue;
            remaining = remaining.Slice(containerUtf8.Length);
            if (remaining.Length < 4 || !remaining.StartsWith(_pathPrefixS))
                continue;
            remaining = remaining.Slice(3);

            int slash1 = remaining.IndexOf((byte)'/');
            if (slash1 <= 0)
                continue;
            ReadOnlySpan<byte> formIdSpan = remaining.Slice(0, slash1);
            remaining = remaining.Slice(slash1 + 1);
            int slash2 = remaining.IndexOf((byte)'/');
            if (slash2 <= 0)
                continue;
            ReadOnlySpan<byte> submissionIdSpan = remaining.Slice(0, slash2);
            ReadOnlySpan<byte> fileNameSpan = remaining.Slice(slash2 + 1);
            int q = fileNameSpan.IndexOf((byte)'?');
            if (q >= 0)
                fileNameSpan = fileNameSpan.Slice(0, q);

            if (fileNameSpan.IsEmpty)
                continue;

            if (!TryParseLongUtf8(formIdSpan, out long urlFormId) || urlFormId != formId)
                continue;
            if (!TryParseLongUtf8(submissionIdSpan, out long urlSubmissionId) || urlSubmissionId != submissionId)
                continue;

            string fileName = Encoding.UTF8.GetString(fileNameSpan);
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
            return false;
        for (int i = 0; i < span.Length; i++)
        {
            byte b = span[i];
            if (b < (byte)'0' || b > (byte)'9')
                return false;
            value = value * 10 + (b - (byte)'0');
        }
        return true;
    }
}
