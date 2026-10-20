
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Helpers;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.Submissions.PartialUpdate;

/// <summary>
/// Handler for partially updating a form submission.
/// </summary>
public class PartialUpdateSubmissionHandler(IRepository<Submission> repository, IRepository<SubmissionVersion> versions, IMediator mediator) : ICommandHandler<PartialUpdateSubmissionCommand, Result<Submission>>
{
    private const int DEFAULT_CURRENT_PAGE = 0;
    private const string SUBMISSION_NOT_FOUND_MESSAGE = "Form submission not found.";

    private SubmissionChangeSaver Saver => new(repository, versions);

    public async Task<Result<Submission>> Handle(PartialUpdateSubmissionCommand request, CancellationToken cancellationToken)
    {
        var mustPublishEvent = false;
        var submissionSpec = new SubmissionByFormIdAndSubmissionIdSpec(request.FormId, request.SubmissionId);
        var submission = await repository.SingleOrDefaultAsync(submissionSpec, cancellationToken);
        if (submission == null)
        {
            return Result.NotFound(SUBMISSION_NOT_FOUND_MESSAGE);
        }

        var screenOut = CollectionOutcomes.IsScreenOut(request.CollectionOutcome);
        var wasScreenedOut = submission.IsScreenedOut;
        if (!screenOut && !submission.IsComplete && (request.IsComplete ?? false))
        {
            mustPublishEvent = true;
        }

        // Each given property replaces the stored one whole, JsonData included; only Metadata is merged, at its top level.
        // IsComplete and CurrentPage come from the request as they are, not derived from the answers.
        var originalJson = submission.JsonData;
        var jsonData = request.JsonData ?? submission.JsonData;
        var currentPage = request.CurrentPage ?? submission.CurrentPage ?? DEFAULT_CURRENT_PAGE;
        var mergedMetadata = request.Metadata != null
            ? JsonHelpers.MergeTopLevelObject(submission.Metadata, request.Metadata)
            : submission.Metadata;

        try
        {
            if (screenOut)
            {
                // A screen-out never completes; a repeat (e.g. a retried request) is a no-op.
                submission.ScreenOut(jsonData, submission.FormDefinitionId, submission.FormId, currentPage, mergedMetadata);
            }
            else
            {
                submission.Update(
                    jsonData,
                    submission.FormDefinitionId,
                    submission.FormId,
                    request.IsComplete ?? submission.IsComplete,
                    currentPage,
                    mergedMetadata
                );
            }
        }
        catch (InvalidOperationException)
        {
            return Result.Invalid(new ValidationError(RejectionMessage(wasScreenedOut, screenOut)));
        }

        var saved = await Saver.TrySaveAsync(submission, originalJson, cancellationToken);
        if (!saved.IsSuccess)
        {
            return saved.ToErrorResult<Submission>();
        }

        if (mustPublishEvent)
        {
            await mediator.Publish(new SubmissionCompletedEvent(submission), cancellationToken);
        }

        return Result.Success(submission);
    }

    private static string RejectionMessage(bool wasScreenedOut, bool screenOut)
    {
        if (wasScreenedOut)
        {
            return CollectionOutcomes.SCREENED_OUT_EDIT_REJECTED_MESSAGE;
        }

        if (screenOut)
        {
            return "This submission cannot be changed.";
        }

        return "This submission cannot be completed.";
    }
}
