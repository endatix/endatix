using System.IO.Compression;
using FastEndpoints;
using MediatR;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Core.Abstractions.Repositories;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for downloading files for a submission.
/// </summary>
public class GetSubmissionFiles(IMediator mediator, IHttpClientFactory httpClientFactory, IRepository<Submission> submissionRepository, IFormsRepository formRepository) : Endpoint<GetSubmissionFilesRequest>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("forms/{formId}/submissions/{submissionId}/files");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Download files for a submission";
            s.Description = "Downloads use uploaded files for a given submission";
            s.Responses[200] = "The files were successfully downloaded";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found. Cannot download files";
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(GetSubmissionFilesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Fetch submission by formId and submissionId
            var spec = new SubmissionWithDefinitionSpec(request.FormId, request.SubmissionId);
            var submission = await submissionRepository.SingleOrDefaultAsync(spec, cancellationToken);
            var form = await formRepository.GetByIdAsync(request.FormId, cancellationToken);

            if (submission is null || form is null)
            {
                await SendNotFoundAsync();
                return;
            }

            // Parse submission.JsonData
            using var doc = JsonDocument.Parse(submission.JsonData);
            var files = new List<(string fileName, string mimeType, Stream stream)>();
            var httpClient = httpClientFactory.CreateClient();

            var prefix = SanitizeFileName(request.FileNamesPrefix ?? string.Empty);

            // Recursive file extraction
            FindFilesRecursive(doc.RootElement, string.Empty, files, httpClient, cancellationToken, prefix);

            if (files.Count == 0)
            {
                // Return an empty ZIP archive with 200 OK and a custom header
                using var emptyZipStream = new MemoryStream();
                using (var archive = new ZipArchive(emptyZipStream, ZipArchiveMode.Create, true))
                { }
                emptyZipStream.Position = 0;

                HttpContext.Response.Headers["X-Endatix-Empty-Zip"] = "true";
                await SendStreamAsync(emptyZipStream, "application/zip");
                return;
            }

            var zipFileName = $"{ToKebabCase(form.Name)}-{submission.Id}.zip";

            HttpContext.MarkResponseStart();
            HttpContext.Response.StatusCode = 200;
            HttpContext.Response.ContentType = "application/zip";
            HttpContext.Response.Headers["Content-Disposition"] = $"attachment; filename={zipFileName}";

            using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var (fileName, mimeType, stream) in files)
                {
                    var entry = archive.CreateEntry(fileName, CompressionLevel.Fastest);
                    using var entryStream = entry.Open();
                    await stream.CopyToAsync(entryStream, cancellationToken);
                    stream.Dispose();
                }
            }
            zipStream.Seek(0, SeekOrigin.Begin);
            await zipStream.CopyToAsync(HttpContext.Response.Body, cancellationToken);
        }
        catch (Exception ex)
        {
            await SendNotFoundAsync();
        }
    }

    // Recursive file finder
    private static void FindFilesRecursive(JsonElement element, string path, List<(string fileName, string mimeType, Stream stream)> files, HttpClient httpClient, CancellationToken cancellationToken, string prefix)
    {
        if (IsFileObject(element))
        {
            var rootQuestionName = path.Trim('.').Split('.')[0];
            TryExtractFile(rootQuestionName, element, files, httpClient, cancellationToken, prefix, 0, 1);
            return;
        }
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                var newPath = string.IsNullOrEmpty(path) ? prop.Name : $"{path}.{prop.Name}";
                FindFilesRecursive(prop.Value, newPath, files, httpClient, cancellationToken, prefix);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var idx = 0;
            foreach (var item in element.EnumerateArray())
            {
                FindFilesRecursive(item, path, files, httpClient, cancellationToken, prefix);
                idx++;
            }
        }
    }

    // Helper to check if an element is a file object
    private static bool IsFileObject(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("name", out _)
            && element.TryGetProperty("type", out _)
            && element.TryGetProperty("content", out _);
    }

    private static void TryExtractFile(string questionName, JsonElement fileElement, List<(string fileName, string mimeType, Stream stream)> files, HttpClient httpClient, CancellationToken cancellationToken, string prefix, int index, int total)
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
        var fileName = SanitizeFileName(baseName) + ext;
        var mimeType = typeProp.GetString() ?? "application/octet-stream";
        var content = contentProp.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        if (content.StartsWith("http://") || content.StartsWith("https://"))
        {
            var response = httpClient.GetAsync(content, HttpCompletionOption.ResponseHeadersRead, cancellationToken).GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                var stream = response.Content.ReadAsStreamAsync(cancellationToken).GetAwaiter().GetResult();
                files.Add((fileName, mimeType, stream));
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
                    files.Add((fileName, mimeType, new MemoryStream(bytes)));
                }
                catch { }
            }
        }
        else
        {
            try
            {
                var bytes = Convert.FromBase64String(content);
                files.Add((fileName, mimeType, new MemoryStream(bytes)));
            }
            catch { }
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }

        return fileName;
    }

    private static string ToKebabCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        ReadOnlySpan<char> src = input;
        // For most names, 2x length is plenty (worst case: every char becomes "-x")
        Span<char> buffer = src.Length <= 128 ? stackalloc char[src.Length * 2] : new char[src.Length * 2];
        var pos = 0;
        var wasLower = false;

        for (var i = 0; i < src.Length; i++)
        {
            var c = src[i];
            if (char.IsWhiteSpace(c) || c == '_' || c == '-')
            {
                if (pos > 0 && buffer[pos - 1] != '-')
                {
                    buffer[pos++] = '-';
                }

                wasLower = false;
            }
            else if (char.IsUpper(c))
            {
                if (wasLower && pos > 0 && buffer[pos - 1] != '-')
                {
                    buffer[pos++] = '-';
                }

                buffer[pos++] = char.ToLowerInvariant(c);
                wasLower = false;
            }
            else
            {
                buffer[pos++] = c;
                wasLower = true;
            }
        }

        // Remove trailing dash
        if (pos > 0 && buffer[pos - 1] == '-')
        {
            pos--;
        }

        // Remove leading dash
        var start = 0;
        if (pos > 0 && buffer[0] == '-')
        {
            start = 1;
        }

        return new string(buffer[start..pos]);
    }
}