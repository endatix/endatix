using System.IO.Compression;
using FastEndpoints;
using MediatR;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Infrastructure.Utils;
using Endatix.Infrastructure.Features.Submissions;

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
            var httpClient = httpClientFactory.CreateClient();
            var prefix = FileNameHelper.SanitizeFileName(request.FileNamesPrefix ?? string.Empty);

            var extractor = new SubmissionFileExtractor(httpClient);
            var extractedFiles = extractor.ExtractFiles(doc.RootElement, prefix, cancellationToken);

            if (extractedFiles.Count == 0)
            {
                // Return an empty ZIP archive with 200 OK and a custom header
                using var emptyZipStream = new MemoryStream();
                using (var archive = new ZipArchive(emptyZipStream, ZipArchiveMode.Create, true))
                { }
                emptyZipStream.Position = 0;

                HttpContext.Response.Headers["X-Endatix-Empty-File"] = "true";
                await SendStreamAsync(emptyZipStream, "application/zip");
                return;
            }

            var zipFileName = $"{StringUtils.ToKebabCase(form.Name)}-{submission.Id}.zip";

            HttpContext.MarkResponseStart();
            HttpContext.Response.StatusCode = 200;
            HttpContext.Response.ContentType = "application/zip";
            HttpContext.Response.Headers["Content-Disposition"] = $"attachment; filename={zipFileName}";

            using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var (fileName, mimeType, stream) in extractedFiles)
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
}