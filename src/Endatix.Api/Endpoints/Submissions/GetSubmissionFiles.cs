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

            // Sanitize and use the prefix if provided
            var prefix = SanitizeFileName(request.FileNamesPrefix ?? string.Empty);

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        TryExtractFile(property.Name, item, files, httpClient, cancellationToken, prefix);
                    }
                }
                else if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    TryExtractFile(property.Name, property.Value, files, httpClient, cancellationToken, prefix);
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

    private static void TryExtractFile(string questionName, JsonElement fileElement, List<(string fileName, string mimeType, Stream stream)> files, HttpClient httpClient, CancellationToken cancellationToken, string prefix)
    {
        if (!fileElement.TryGetProperty("name", out var nameProp) ||
            !fileElement.TryGetProperty("type", out var typeProp) ||
            !fileElement.TryGetProperty("content", out var contentProp))
        {
            return;
        }

        var fileName = SanitizeFileName(questionName + "-" + nameProp.GetString() ?? "file");
        var mimeType = typeProp.GetString() ?? "application/octet-stream";
        var content = contentProp.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        // Prepend prefix if provided
        if (!string.IsNullOrEmpty(prefix))
        {
            fileName = prefix + fileName;
        }

        if (content.StartsWith("http://") || content.StartsWith("https://"))
        {
            // Download from URL (sync-over-async for simplicity in this static method)
            var response = httpClient.GetAsync(content, HttpCompletionOption.ResponseHeadersRead, cancellationToken).GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                var stream = response.Content.ReadAsStreamAsync(cancellationToken).GetAwaiter().GetResult();
                files.Add((fileName, mimeType, stream));
            }
        }
        else if (content.StartsWith("data:"))
        {
            // data:[<mediatype>][;base64],<data>
            var base64Index = content.IndexOf(",", StringComparison.Ordinal);
            if (base64Index > 0)
            {
                var base64 = content[(base64Index + 1)..];
                try
                {
                    var bytes = Convert.FromBase64String(base64);
                    files.Add((fileName, mimeType, new MemoryStream(bytes)));
                }
                catch { /* skip invalid base64 */ }
            }
        }
        else
        {
            // Try to decode as base64 directly
            try
            {
                var bytes = Convert.FromBase64String(content);
                files.Add((fileName, mimeType, new MemoryStream(bytes)));
            }
            catch { /* skip invalid base64 */ }
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
