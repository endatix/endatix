
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
        var submissionSpec = new SubmissionByFormIdAndSubmissionIdSpec(request.FormId, request.SubmissionId);
        var submission = await repository.SingleOrDefaultAsync(submissionSpec, cancellationToken);
        if (submission == null)
        {
            return Result.NotFound(SUBMISSION_NOT_FOUND_MESSAGE);
        }

        if (!CollectionOutcomes.IsSupported(request.CollectionOutcome, out var outcome))
        {
            return Result.Invalid(new ValidationError(CollectionOutcomes.UNSUPPORTED_MESSAGE));
        }

        var previousStatus = submission.CollectionStatus;
        var mustPublishEvent = outcome is null && !submission.IsComplete && (request.IsComplete ?? false);

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
            if (outcome is not null)
            {
                // An outcome never completes; a repeat (e.g. a retried request) is a no-op.
                submission.Close(outcome, new SubmissionContent(jsonData, submission.FormDefinitionId, currentPage, mergedMetadata), submission.FormId);
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
            return Result.Invalid(new ValidationError(RejectionMessage(previousStatus, outcome is not null)));
        }

        var saved = await Saver.TrySaveAsync(submission, originalJson, cancellationToken);
        if (!saved.IsSuccess)
        {
            return saved.ToErrorResult<Submission>();
        }

        if (mustPublishEvent)
        {
            await mediator.PublishCompletedAsync(submission, cancellationToken);
        }

        return Result.Success(submission);
    }

    private static string RejectionMessage(CollectionStatus previous, bool closing)
    {
        if (CollectionOutcomes.RejectedEditMessage(previous) is { } closedMessage)
        {
            return closedMessage;
        }

        if (closing)
        {
            return "This submission cannot be changed.";
        }

        return "This submission cannot be completed.";
    }
}
