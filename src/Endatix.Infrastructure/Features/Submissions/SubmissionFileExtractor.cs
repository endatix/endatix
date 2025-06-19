using System.Text.Json;
using Endatix.Core.Abstractions.Submissions;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.Submissions;

public sealed class SubmissionFileExtractor : ISubmissionFileExtractor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SubmissionFileExtractor> _logger;

    public SubmissionFileExtractor(IHttpClientFactory httpClientFactory, ILogger<SubmissionFileExtractor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<List<ISubmissionFileExtractor.ExtractedFile>> ExtractFilesAsync(
        JsonElement root, long submissionId, string prefix = "", CancellationToken cancellationToken = default)
    {
        var files = new List<ISubmissionFileExtractor.ExtractedFile>();
        var context = new ExtractionContext(prefix, submissionId, cancellationToken, files);
        await FindFilesRecursiveAsync(root, string.Empty, context);
        return files;
    }

    private class ExtractionContext
    {
        public string Prefix { get; }
        public long SubmissionId { get; }
        public CancellationToken CancellationToken { get; }
        public List<ISubmissionFileExtractor.ExtractedFile> Files { get; }
        public ExtractionContext(string prefix, long submissionId, CancellationToken token, List<ISubmissionFileExtractor.ExtractedFile> files)
        {
            Prefix = prefix;
            SubmissionId = submissionId;
            CancellationToken = token;
            Files = files;
        }
    }

    private async Task FindFilesRecursiveAsync(JsonElement element, string path, ExtractionContext context)
    {
        if (IsFileObject(element))
        {
            var rootQuestionName = path.Trim('.').Split('.')[0];
            await TryExtractFileAsync(rootQuestionName, element, context, 0, 1);
            return;
        }
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                var newPath = string.IsNullOrEmpty(path) ? prop.Name : $"{path}.{prop.Name}";
                await FindFilesRecursiveAsync(prop.Value, newPath, context);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var array = element.EnumerateArray().ToList();
            for (var idx = 0; idx < array.Count; idx++)
            {
                var item = array[idx];
                if (IsFileObject(item))
                {
                    var rootQuestionName = path.Trim('.').Split('.')[0];
                    await TryExtractFileAsync(rootQuestionName, item, context, idx + 1, array.Count);
                }
                else
                {
                    await FindFilesRecursiveAsync(item, path, context);
                }
            }
        }
    }

    private static bool IsFileObject(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("name", out _)
            && element.TryGetProperty("type", out _)
            && element.TryGetProperty("content", out _);
    }

    /// <summary>
    /// Attempts to extract a file from a file JSON element and add it to the extraction context, handling different content encodings.
    /// </summary>
    /// <param name="questionName">The root question name associated with the file.</param>
    /// <param name="fileElement">The JSON element representing the file object.</param>
    /// <param name="context">The extraction context containing state and results.</param>
    /// <param name="index">The index of the file in an array, if applicable.</param>
    /// <param name="total">The total number of files in the array, if applicable.</param>
    private async Task TryExtractFileAsync(
        string questionName, JsonElement fileElement, ExtractionContext context, int index, int total)
    {
        if (!fileElement.TryGetProperty("name", out var nameProp) ||
            !fileElement.TryGetProperty("type", out var typeProp) ||
            !fileElement.TryGetProperty("content", out var contentProp))
        {
            return;
        }

        var originalName = nameProp.GetString() ?? "file";
        var ext = Path.GetExtension(originalName);
        var baseName = string.IsNullOrEmpty(context.Prefix) ? "" : context.Prefix + "-";
        baseName += questionName;
        if (total > 1)
        {
            baseName += $"-{index}";
        }
        var fileName = Utils.FileNameHelper.SanitizeFileName(baseName) + ext;
        var mimeType = typeProp.GetString() ?? "application/octet-stream";
        var content = contentProp.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        if (content.StartsWith("http://") || content.StartsWith("https://"))
        {
            await ExtractFromUrlAsync(content, fileName, mimeType, context);
        }
        else if (content.StartsWith("data:"))
        {
            ExtractFromDataUri(content, fileName, mimeType, context);
        }
        else
        {
            ExtractFromBase64(content, fileName, mimeType, context);
        }
    }

    /// <summary>
    /// Downloads a file from a URL and adds it to the extraction context.
    /// </summary>
    /// <param name="url">The URL to fetch the file from.</param>
    /// <param name="fileName">The sanitized file name for the extracted file.</param>
    /// <param name="mimeType">The MIME type of the file.</param>
    /// <param name="context">The extraction context containing state and results.</param>
    private async Task ExtractFromUrlAsync(string url, string fileName, string mimeType, ExtractionContext context)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, context.CancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var stream = await response.Content.ReadAsStreamAsync(context.CancellationToken);
                context.Files.Add(new ISubmissionFileExtractor.ExtractedFile(fileName, mimeType, stream));
            }
            else
            {
                _logger.LogWarning("Failed to fetch file from URL: {Url} (Status: {StatusCode}, SubmissionId: {SubmissionId})", url, response.StatusCode, context.SubmissionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while fetching file from URL: {Url} (FileName: {FileName}, MimeType: {MimeType}, SubmissionId: {SubmissionId})", url, fileName, mimeType, context.SubmissionId);
        }
    }

    /// <summary>
    /// Decodes a file from a data URI and adds it to the extraction context.
    /// </summary>
    /// <param name="dataUri">The data URI string containing base64-encoded file data.</param>
    /// <param name="fileName">The sanitized file name for the extracted file.</param>
    /// <param name="mimeType">The MIME type of the file.</param>
    /// <param name="context">The extraction context containing state and results.</param>
    private void ExtractFromDataUri(string dataUri, string fileName, string mimeType, ExtractionContext context)
    {
        var base64Index = dataUri.IndexOf(",", StringComparison.Ordinal);
        if (base64Index > 0)
        {
            var base64 = dataUri[(base64Index + 1)..];
            try
            {
                var bytes = Convert.FromBase64String(base64);
                context.Files.Add(new ISubmissionFileExtractor.ExtractedFile(fileName, mimeType, new MemoryStream(bytes)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decode base64 from data URI (FileName: {FileName}, MimeType: {MimeType}, SubmissionId: {SubmissionId})", fileName, mimeType, context.SubmissionId);
            }
        }
        else
        {
            _logger.LogWarning("Invalid data URI format (FileName: {FileName}, MimeType: {MimeType}, SubmissionId: {SubmissionId})", fileName, mimeType, context.SubmissionId);
        }
    }

    /// <summary>
    /// Decodes a file from a raw base64 string and adds it to the extraction context.
    /// </summary>
    /// <param name="base64">The base64-encoded file content.</param>
    /// <param name="fileName">The sanitized file name for the extracted file.</param>
    /// <param name="mimeType">The MIME type of the file.</param>
    /// <param name="context">The extraction context containing state and results.</param>
    private void ExtractFromBase64(string base64, string fileName, string mimeType, ExtractionContext context)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64);
            context.Files.Add(new ISubmissionFileExtractor.ExtractedFile(fileName, mimeType, new MemoryStream(bytes)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decode base64 content (FileName: {FileName}, MimeType: {MimeType}, SubmissionId: {SubmissionId})", fileName, mimeType, context.SubmissionId);
        }
    }
}