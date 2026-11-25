using Endatix.Core.Abstractions;
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
[DirectCallOnly]
public class UpdateSubmissionHandler : ICommandHandler<UpdateSubmissionCommand, Result<Submission>>
{
    private readonly SubmissionTargets _targets;
    private readonly SubmissionChangeSaver _saver;
    private readonly IMediator _mediator;

    /// <summary>Uses the supplied repositories for direct calls.</summary>
    public UpdateSubmissionHandler(IRepository<Submission> repository, IRepository<SubmissionVersion> versions, IMediator mediator)
        : this(new SubmissionTargets(repository, ContentQueryScope.Repository), new SubmissionChangeSaver(repository, versions), mediator) { }

    private UpdateSubmissionHandler(SubmissionTargets targets, SubmissionChangeSaver saver, IMediator mediator)
    {
        _targets = targets;
        _saver = saver;
        _mediator = mediator;
    }

    private const bool DEFAULT_IS_COMPLETE = false;
    private const int DEFAULT_CURRENT_PAGE = 1;
    private const string DEFAULT_METADATA = null;
    private const string SUBMISSION_NOT_FOUND_MESSAGE = "Form submission not found.";

    /// <inheritdoc/>
    public async Task<Result<Submission>> Handle(UpdateSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submission = await _targets.FindAsync(request.FormId, request.SubmissionId, cancellationToken);

        if (submission == null)
        {
            return Result.NotFound(SUBMISSION_NOT_FOUND_MESSAGE);
        }

        var mustPublishEvent = !submission.IsComplete && (request.IsComplete ?? false);

        var originalJson = submission.JsonData;
        var previousStatus = submission.CollectionStatus;

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
            var message = CollectionOutcomes.RejectedEditMessage(previousStatus) ?? "This submission cannot be completed.";
            return Result.Invalid(new ValidationError(message));
        }

        var saved = await _saver.TrySaveAsync(submission, originalJson, cancellationToken);
        if (!saved.IsSuccess)
        {
            return saved.ToErrorResult<Submission>();
        }

        if (mustPublishEvent)
        {
            await _mediator.PublishCompletedAsync(submission, cancellationToken);
        }

        return Result.Success(submission);
    }

    internal sealed class Registered(SubmissionTargets targets, SubmissionChangeSaver saver, IMediator mediator)
        : RegisteredContentHandler<UpdateSubmissionCommand, Result<Submission>>(
            new UpdateSubmissionHandler(targets, saver, mediator));
}
