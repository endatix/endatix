using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions.GetFiles;

[DirectCallOnly]
public sealed class GetFilesHandler : IQueryHandler<GetFilesQuery, Result<GetFilesResult>>
{
    private readonly SubmissionFileTargets _targets;
    private readonly ISubmissionFileExtractor _fileExtractor;

    /// <summary>Uses the supplied repositories for direct calls.</summary>
    public GetFilesHandler(IRepository<Submission> submissionRepository, IFormsRepository formRepository, ISubmissionFileExtractor fileExtractor)
        : this(new SubmissionFileTargets(submissionRepository, formRepository, ContentQueryScope.Repository), fileExtractor) { }

    private GetFilesHandler(SubmissionFileTargets targets, ISubmissionFileExtractor fileExtractor)
    {
        _targets = targets;
        _fileExtractor = fileExtractor;
    }

    public async Task<Result<GetFilesResult>> Handle(GetFilesQuery request, CancellationToken cancellationToken)
    {
        var (submission, form) = await _targets.ReadAsync(request, cancellationToken);

        if (submission is null || form is null)
        {
            return Result.NotFound("Form or submission not found");
        }

        using var doc = System.Text.Json.JsonDocument.Parse(submission.JsonData);
        var files = new List<FileDescriptor>();
        var extracted = await _fileExtractor.ExtractFilesAsync(
            root: doc.RootElement,
            formId: request.FormId,
            submissionId: submission.Id,
            prefix: request.FileNamesPrefix ?? string.Empty,
            cancellationToken: cancellationToken);

        // Convert the extracted files to FileDescriptor objects
        foreach (var f in extracted)
        {
            files.Add(new FileDescriptor(f.FileName, f.MimeType, f.Content));
        }

        var result = new GetFilesResult(form.Name, submission.Id, files);
        return Result.Success(result);
    }

    internal sealed class Registered(SubmissionFileTargets targets, ISubmissionFileExtractor fileExtractor)
        : RegisteredContentHandler<GetFilesQuery, Result<GetFilesResult>>(
            new GetFilesHandler(targets, fileExtractor));
}
