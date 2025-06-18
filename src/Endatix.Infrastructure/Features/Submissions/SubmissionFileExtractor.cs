using System.Text.Json;
using Endatix.Core.Abstractions.Submissions;

namespace Endatix.Infrastructure.Features.Submissions;

public sealed class SubmissionFileExtractor : ISubmissionFileExtractor
{
    private readonly IHttpClientFactory _httpClientFactory;

    public SubmissionFileExtractor(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<ISubmissionFileExtractor.ExtractedFile>> ExtractFilesAsync(
        JsonElement root, string prefix = "", CancellationToken cancellationToken = default)
    {
        var files = new List<ISubmissionFileExtractor.ExtractedFile>();
        var context = new ExtractionContext(prefix, cancellationToken, files);
        await FindFilesRecursiveAsync(root, string.Empty, context);
        return files;
    }

    private class ExtractionContext
    {
        public string Prefix { get; }
        public CancellationToken CancellationToken { get; }
        public List<ISubmissionFileExtractor.ExtractedFile> Files { get; }
        public ExtractionContext(string prefix, CancellationToken token, List<ISubmissionFileExtractor.ExtractedFile> files)
        {
            Prefix = prefix;
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
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(content, HttpCompletionOption.ResponseHeadersRead, context.CancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var stream = await response.Content.ReadAsStreamAsync(context.CancellationToken);
                context.Files.Add(new ISubmissionFileExtractor.ExtractedFile(fileName, mimeType, stream));
            }
        }
        else if (content.StartsWith("data:"))
        {
            var base64Index = content.IndexOf(",", StringComparison.Ordinal);
            if (base64Index > 0)
            {
                var base64 = content[(base64Index + 1)..];
                try
                {
                    var bytes = Convert.FromBase64String(base64);
                    context.Files.Add(new ISubmissionFileExtractor.ExtractedFile(fileName, mimeType, new MemoryStream(bytes)));
                }
                catch { }
            }
        }
        else
        {
            try
            {
                var bytes = Convert.FromBase64String(content);
                context.Files.Add(new ISubmissionFileExtractor.ExtractedFile(fileName, mimeType, new MemoryStream(bytes)));
            }
            catch { }
        }
    }
}