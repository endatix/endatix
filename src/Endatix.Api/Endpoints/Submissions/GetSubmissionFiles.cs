using System.IO.Compression;
using FastEndpoints;
using MediatR;
using Endatix.Infrastructure.Utils;
using Endatix.Core.UseCases.Submissions.GetFiles;
using FluentValidation.Results;
using Endatix.Core.Abstractions.Authorization;
using Microsoft.Extensions.Logging;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for downloading files for a submission.
/// </summary>
public class GetSubmissionFiles(IMediator mediator, ILogger<GetSubmissionFiles> logger) : Endpoint<GetSubmissionFilesRequest>
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
            await SendFilesAsync(request, cancellationToken);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            AbortIfResponseStarted();
        }
        catch (Exception ex)
        {
            await SendFailureAsync(ex, cancellationToken);
        }
    }

    private async Task SendFilesAsync(GetSubmissionFilesRequest request, CancellationToken cancellationToken)
    {
        var query = new GetFilesQuery(request.FormId, request.SubmissionId, request.FileNamesPrefix);
        var result = await mediator.Send(query, cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        using var zipStream = await BuildZipAsync(result.Value.Files, cancellationToken);
        if (result.Value.Files.Count == 0)
        {
            await SendEmptyZipAsync(zipStream, cancellationToken);
            return;
        }

        await SendZipAsync(zipStream, result.Value, cancellationToken);
    }

    private async Task SendEmptyZipAsync(MemoryStream zipStream, CancellationToken cancellationToken)
    {
        HttpContext.Response.Headers["X-Endatix-Empty-File"] = "true";
        HttpContext.Response.ContentType = "application/zip";
        await Send.StreamAsync(zipStream, contentType: "application/zip", cancellation: cancellationToken);
    }

    private async Task SendZipAsync(MemoryStream zipStream, GetFilesResult filesResult, CancellationToken cancellationToken)
    {
        var zipFileName = $"{StringUtils.ToKebabCase(filesResult.FormName)}-{filesResult.SubmissionId}.zip";
        HttpContext.MarkResponseStart();
        HttpContext.Response.StatusCode = 200;
        HttpContext.Response.ContentType = "application/zip";
        HttpContext.Response.Headers.ContentDisposition = $"attachment; filename={zipFileName}";

        await zipStream.CopyToAsync(HttpContext.Response.Body, cancellationToken);
    }

    private static async Task<MemoryStream> BuildZipAsync(IReadOnlyCollection<FileDescriptor> files, CancellationToken cancellationToken)
    {
        var zipStream = new MemoryStream();
        try
        {
            await using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                await AddEntriesAsync(archive, files, cancellationToken);
            }
        }
        finally
        {
            await DisposeAllAsync(files);
        }

        zipStream.Seek(0, SeekOrigin.Begin);
        return zipStream;
    }

    private static async Task AddEntriesAsync(ZipArchive archive, IEnumerable<FileDescriptor> files, CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            await using var content = file.Content;
            var entry = archive.CreateEntry(file.FileName, CompressionLevel.Fastest);
            await using var entryStream = await entry.OpenAsync(cancellationToken);
            await content.CopyToAsync(entryStream, cancellationToken);
        }
    }

    private static async Task DisposeAllAsync(IEnumerable<FileDescriptor> files)
    {
        foreach (var file in files)
        {
            await file.Content.DisposeAsync();
        }
    }

    private async Task SendFailureAsync(Exception ex, CancellationToken cancellationToken)
    {
        logger.LogError(ex, "Failed to download submission files");

        if (AbortIfResponseStarted())
        {
            return;
        }

        ValidationFailures.Add(new ValidationFailure("error", "Failed to download submission files."));
        await Send.ErrorsAsync(500, cancellationToken);
    }

    private bool AbortIfResponseStarted()
    {
        if (!HttpContext.ResponseStarted())
        {
            return false;
        }

        // Headers and part of the zip are out, so a 500 can't be sent; cut the connection
        // so the client sees a broken download rather than a truncated zip.
        HttpContext.Abort();
        return true;
    }
}
