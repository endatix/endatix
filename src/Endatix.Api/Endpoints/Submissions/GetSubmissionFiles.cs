using System.IO.Compression;
using FastEndpoints;
using MediatR;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for downloading files for a submission.
/// </summary>
public class GetSubmissionFiles(IMediator mediator, IHttpClientFactory httpClientFactory, IRepository<Submission> submissionRepository) : Endpoint<GetSubmissionFilesRequest>
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
            var spec = new SubmissionByFormIdAndSubmissionIdSpec(request.FormId, request.SubmissionId);
            var submission = await submissionRepository.SingleOrDefaultAsync(spec, cancellationToken);
            if (submission is null)
            {
                await SendNotFoundAsync();
                return;
            }

            // Parse submission.JsonData
            using var doc = JsonDocument.Parse(submission.JsonData);
            var files = new List<(string fileName, string mimeType, Stream stream)>();
            var httpClient = httpClientFactory.CreateClient();

            var prefix = SanitizeFileName(request.FileNamesPrefix ?? string.Empty);

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    var items = property.Value.EnumerateArray().ToList();
                    var total = items.Count;
                    for (var i = 0; i < total; i++)
                    {
                        TryExtractFile(property.Name, items[i], files, httpClient, cancellationToken, prefix, i + 1, total);
                    }
                }
                else if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    TryExtractFile(property.Name, property.Value, files, httpClient, cancellationToken, prefix, 0, 1);
                }
            }

            if (files.Count == 0)
            {
                await SendNotFoundAsync();
                return;
            }

            HttpContext.MarkResponseStart();
            HttpContext.Response.StatusCode = 200;
            HttpContext.Response.ContentType = "application/zip";
            HttpContext.Response.Headers["Content-Disposition"] = "attachment; filename=submission-files.zip";

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
        catch (Exception)
        {
            await SendNotFoundAsync();
        }
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
}
