using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.Delete;

public class DeleteSubmissionHandler(IRepository<Submission> repository) : ICommandHandler<DeleteSubmissionCommand, Result<Submission>>
{
    public async Task<Result<Submission>> Handle(DeleteSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submission = await repository.GetByIdAsync(request.SubmissionId, cancellationToken);
        if (submission is null || submission.FormId != request.FormId)
        {
            return Result.NotFound("Submission not found");
        }

        submission.Delete();

        // Deleted meanwhile, on its own or with its form: the outcome the caller asked for, but not this delete's.
        return await repository.TryUpdateAsync(submission, cancellationToken)
            ? Result.Success(submission)
            : Result.NotFound("Submission not found");
    }
}
