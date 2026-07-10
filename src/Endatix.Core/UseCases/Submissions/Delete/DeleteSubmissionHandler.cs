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
        if (submission == null)
        {
            return Result.NotFound("Submission not found");
        }

        // Validate that the submission belongs to the specified form
        if (submission.FormId != request.FormId)
        {
            return Result.NotFound("Submission not found");
        }

        submission.Delete();
        await repository.UpdateAsync(submission, cancellationToken);

        return Result.Success(submission);
    }
}
