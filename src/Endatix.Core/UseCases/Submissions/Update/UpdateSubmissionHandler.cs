using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Handler for updating a form submission.
/// </summary>
public class UpdateSubmissionHandler(IRepository<Submission> repository, IRepository<SubmissionVersion> versions, IMediator mediator) : ICommandHandler<UpdateSubmissionCommand, Result<Submission>>
{
    private const bool DEFAULT_IS_COMPLETE = false;
    private const int DEFAULT_CURRENT_PAGE = 1;
    private const string DEFAULT_METADATA = null;
    private const string SUBMISSION_NOT_FOUND_MESSAGE = "Form submission not found.";

    public async Task<Result<Submission>> Handle(UpdateSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submissionSpec = new SubmissionByFormIdAndSubmissionIdSpec(request.FormId, request.SubmissionId);
        var submission = await repository.SingleOrDefaultAsync(submissionSpec, cancellationToken);
        var mustPublishEvent = false;

        if (submission == null)
        {
            return Result.NotFound(SUBMISSION_NOT_FOUND_MESSAGE);
        }

        if(!submission.IsComplete && (request.IsComplete ?? false)) {
            mustPublishEvent = true;
        }

        var originalJson = submission.JsonData;
        var wasScreenedOut = submission.IsScreenedOut;

        try
        {
            submission.Update(
                request.JsonData,
                submission.FormDefinitionId,
                submission.FormId,
                request.IsComplete ?? DEFAULT_IS_COMPLETE,
                request.CurrentPage ?? DEFAULT_CURRENT_PAGE,
                request.Metadata ?? DEFAULT_METADATA
            );
        }
        catch (InvalidOperationException)
        {
            var message = wasScreenedOut
                ? CollectionOutcomes.SCREENED_OUT_EDIT_REJECTED_MESSAGE
                : "This submission cannot be completed.";
            return Result.Invalid(new ValidationError(message));
        }

        var saver = new SubmissionChangeSaver(repository, versions);
        if (!await saver.TrySaveAsync(submission, originalJson, cancellationToken))
        {
            return Result.NotFound(SUBMISSION_NOT_FOUND_MESSAGE);
        }

        if(mustPublishEvent) {
            await mediator.Publish(new SubmissionCompletedEvent(submission), cancellationToken);
        }

        return Result.Success(submission);
    }
}
