using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.Submissions.Delete;

public class DeleteSubmissionHandler(IRepository<Submission> _repository, IMediator mediator) : ICommandHandler<DeleteSubmissionCommand, Result<Submission>>
{
    public async Task<Result<Submission>> Handle(DeleteSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submission = await _repository.GetByIdAsync(request.SubmissionId, cancellationToken);
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
        await _repository.UpdateAsync(submission, cancellationToken);

        await mediator.Publish(new SubmissionDeletedEvent(submission), cancellationToken);

        return Result.Success(submission);
    }
}