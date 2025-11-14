using System.IO.Compression;
using FastEndpoints;
using MediatR;
using Endatix.Infrastructure.Utils;
using Endatix.Core.UseCases.Submissions.GetFiles;
using FluentValidation.Results;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for downloading files for a submission.
/// </summary>
public class GetSubmissionFiles(IMediator mediator) : Endpoint<GetSubmissionFilesRequest>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Post("forms/{formId}/submissions/{submissionId}/files");
        Permissions(Actions.Submissions.View);
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
            var query = new GetFilesQuery(request.FormId, request.SubmissionId, request.FileNamesPrefix);
            var result = await mediator.Send(query, cancellationToken);

            if (!result.IsSuccess || result.Value is null)
            {
                await Send.NotFoundAsync();
                return;
            }

            var filesResult = result.Value;
            if (filesResult.Files.Count == 0)
            {
                using var emptyZipStream = new MemoryStream();
                using (var archive = new ZipArchive(emptyZipStream, ZipArchiveMode.Create, true))
                { }
                emptyZipStream.Position = 0;
                HttpContext.Response.Headers["X-Endatix-Empty-File"] = "true";
                HttpContext.Response.ContentType = "application/zip";
                await Send.StreamAsync(emptyZipStream, contentType: "application/zip");
                return;
            }

            var zipFileName = $"{StringUtils.ToKebabCase(filesResult.FormName)}-{filesResult.SubmissionId}.zip";

            HttpContext.MarkResponseStart();
            HttpContext.Response.StatusCode = 200;
            HttpContext.Response.ContentType = "application/zip";
            HttpContext.Response.Headers["Content-Disposition"] = $"attachment; filename={zipFileName}";

            using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var file in filesResult.Files)
                {
                    var entry = archive.CreateEntry(file.FileName, CompressionLevel.Fastest);
                    using var entryStream = entry.Open();
                    await file.Content.CopyToAsync(entryStream, cancellationToken);
                    file.Content.Dispose();
                }
            }

            zipStream.Seek(0, SeekOrigin.Begin);
            await zipStream.CopyToAsync(HttpContext.Response.Body, cancellationToken);
        }
        catch (Exception ex)
        {
            ValidationFailures.Add(new ValidationFailure("error", ex.Message));
            await Send.ErrorsAsync(500, cancellationToken);
        }
    }
}