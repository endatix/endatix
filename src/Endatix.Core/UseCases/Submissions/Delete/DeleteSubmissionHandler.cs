using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions.Delete;

/// <summary>Deletes a submission on its live parent form in the current tenant.</summary>
[DirectCallOnly]
public class DeleteSubmissionHandler(IRepository<Submission> repository) : ICommandHandler<DeleteSubmissionCommand, Result<Submission>>
{
    private ContentQueryScope Selection { get; init; } = ContentQueryScope.Repository;

    public async Task<Result<Submission>> Handle(DeleteSubmissionCommand request, CancellationToken cancellationToken)
    {
        // A submission of a deleted form is not found, as for every other write.
        var spec = Selection.Submission(new SubmissionByFormIdAndSubmissionIdSpec(request.FormId, request.SubmissionId));
        var submission = await repository.SingleOrDefaultAsync(spec, cancellationToken);
        if (submission is null)
        {
            return Result.NotFound("Submission not found");
        }

        submission.Delete();

        // Deleted meanwhile, on its own or with its form: the outcome the caller asked for, but not this delete's.
        var saved = await repository.TrySaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Success(submission) : saved.ToErrorResult<Submission>();
    }

    internal sealed class Registered(IRepository<Submission> repository, ContentQueryScope tenant)
        : RegisteredContentHandler<DeleteSubmissionCommand, Result<Submission>>(
            new DeleteSubmissionHandler(repository) { Selection = tenant });
}
