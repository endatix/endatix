
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;
using Endatix.Core.Helpers;

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

        if (!submission.IsComplete && (request.IsComplete ?? false))
        {
            mustPublishEvent = true;
        }

        // TODO: add more advanced PATCH-ing where we can not only replace individual properties, but merge, remove and other typical operations. This is valid especially for the JSON based JsonData and Metadata properties, so we can keep payloads and client logic light, e.g. submit one answer at a time and update JsonData
        // TODO: investigate if IsComplete and CurrentPage should be auto calculated as part of processing the submission
        var originalJson = submission.JsonData;
        var mergedMetadata = JsonHelpers.MergeTopLevelObject(submission.Metadata, request.Metadata);

        submission.Update(
            request.JsonData ?? submission.JsonData,
            submission.FormDefinitionId,
            request.IsComplete ?? submission.IsComplete,
            request.CurrentPage ?? submission.CurrentPage ?? DEFAULT_CURRENT_PAGE,
            mergedMetadata
        );

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
}
