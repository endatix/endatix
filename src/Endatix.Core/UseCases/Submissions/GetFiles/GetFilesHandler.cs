using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.UseCases.Submissions.GetFiles;

public sealed class GetFilesHandler : IQueryHandler<GetFilesQuery, Result<GetFilesResult>>
{
    private readonly IRepository<Submission> _submissionRepository;
    private readonly IFormsRepository _formRepository;
    private readonly ISubmissionFileExtractor _fileExtractor;

    public GetFilesHandler(
        IRepository<Submission> submissionRepository,
        IFormsRepository formRepository,
        ISubmissionFileExtractor fileExtractor
    )
    {
        _submissionRepository = submissionRepository;
        _formRepository = formRepository;
        _fileExtractor = fileExtractor;
    }

    public async Task<Result<GetFilesResult>> Handle(GetFilesQuery request, CancellationToken cancellationToken)
    {
        var spec = new SubmissionWithDefinitionSpec(request.FormId, request.SubmissionId);
        var submission = await _submissionRepository.SingleOrDefaultAsync(spec, cancellationToken);
        var form = await _formRepository.GetByIdAsync(request.FormId, cancellationToken);

        if (submission is null || form is null)
        {
            return Result.NotFound("Form or submission not found");
        }

        using var doc = System.Text.Json.JsonDocument.Parse(submission.JsonData);
        var files = new List<FileDescriptor>();
        var extracted = await _fileExtractor.ExtractFilesAsync(doc.RootElement, submission.Id, request.FileNamesPrefix ?? string.Empty, cancellationToken);
        foreach (var f in extracted)
        {
            files.Add(new FileDescriptor(f.FileName, f.MimeType, f.Content));
        }

        var result = new GetFilesResult(form.Name, submission.Id, files);
        return Result.Success(result);
    }
}