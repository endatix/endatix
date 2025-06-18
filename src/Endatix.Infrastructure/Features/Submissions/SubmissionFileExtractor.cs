using System.Text.Json;

namespace Endatix.Infrastructure.Features.Submissions;

public sealed class SubmissionFileExtractor
{
    private readonly HttpClient _httpClient;

    public SubmissionFileExtractor(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public record ExtractedFile(string FileName, string MimeType, Stream Content);

    public List<ExtractedFile> ExtractFiles(JsonElement root, string prefix = "", CancellationToken cancellationToken = default)
    {
        var files = new List<ExtractedFile>();
        FindFilesRecursive(root, string.Empty, files, prefix, cancellationToken);
        return files;
    }

    private void FindFilesRecursive(JsonElement element, string path, List<ExtractedFile> files, string prefix, CancellationToken cancellationToken)
    {
        if (IsFileObject(element))
        {
            var rootQuestionName = path.Trim('.').Split('.')[0];
            TryExtractFile(rootQuestionName, element, files, prefix, cancellationToken, 0, 1);
            return;
        }
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                var newPath = string.IsNullOrEmpty(path) ? prop.Name : $"{path}.{prop.Name}";
                FindFilesRecursive(prop.Value, newPath, files, prefix, cancellationToken);
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
                    TryExtractFile(rootQuestionName, item, files, prefix, cancellationToken, idx + 1, array.Count);
                }
                else
                {
                    FindFilesRecursive(item, path, files, prefix, cancellationToken);
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

    private void TryExtractFile(string questionName, JsonElement fileElement, List<ExtractedFile> files, string prefix, CancellationToken cancellationToken, int index, int total)
    {
        if (!fileElement.TryGetProperty("name", out var nameProp) ||
            !fileElement.TryGetProperty("type", out var typeProp) ||
            !fileElement.TryGetProperty("content", out var contentProp))
        {
            return;
        }

        var originalName = nameProp.GetString() ?? "file";
        var ext = Path.GetExtension(originalName);
        var baseName = string.IsNullOrEmpty(prefix) ? "" : prefix + "-";
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
            var response = _httpClient.GetAsync(content, HttpCompletionOption.ResponseHeadersRead, cancellationToken).GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                var stream = response.Content.ReadAsStreamAsync(cancellationToken).GetAwaiter().GetResult();
                files.Add(new ExtractedFile(fileName, mimeType, stream));
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
                    files.Add(new ExtractedFile(fileName, mimeType, new MemoryStream(bytes)));
                }
                catch { }
            }
        }
        else
        {
            try
            {
                var bytes = Convert.FromBase64String(content);
                files.Add(new ExtractedFile(fileName, mimeType, new MemoryStream(bytes)));
            }
            catch { }
        }
    }
} 