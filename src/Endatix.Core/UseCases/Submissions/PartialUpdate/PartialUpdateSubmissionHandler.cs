
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

    public async Task<Result<Submission>> Handle(PartialUpdateSubmissionCommand request, CancellationToken cancellationToken)
    {
        var mustPublishEvent = false;
        var submissionSpec = new SubmissionByFormIdAndSubmissionIdSpec(request.FormId, request.SubmissionId);
        var submission = await repository.SingleOrDefaultAsync(submissionSpec, cancellationToken);
        if (submission == null)
        {
            return Result.NotFound("Form submission not found.");
        }

        var screenOut = CollectionOutcomes.IsScreenOut(request.CollectionOutcome);
        var wasScreenedOut = submission.IsScreenedOut;
        if (!screenOut && !submission.IsComplete && (request.IsComplete ?? false))
        {
            mustPublishEvent = true;
        }

        // TODO: add more advanced PATCH-ing where we can not only replace individual properties, but merge, remove and other typical operations. This is valid especially for the JSON based JsonData and Metadata properties, so we can keep payloads and client logic light, e.g. submit one answer at a time and update JsonData
        // TODO: investigate if IsComplete and CurrentPage should be auto calculated as part of processing the submission
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

        if (!string.Equals(originalJson, submission.JsonData, StringComparison.Ordinal))
        {
            var effectiveTimestamp = submission.ModifiedAt ?? submission.CreatedAt;
            var version = new SubmissionVersion(
                submission.Id,
                originalJson,
                effectiveTimestamp
            );

            await versions.AddAsync(version, cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);

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
