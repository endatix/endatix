using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.Submissions.UpdateStatus;

/// <summary>
/// Handles updating the status of a submission.
/// </summary>
public class UpdateStatusHandler(
    IRepository<Submission> submissionRepository,
    ILogger<UpdateStatusHandler> logger
) : ICommandHandler<UpdateStatusCommand, Result<SubmissionDto>>
{
    /// <summary>
    /// Updates the status of a submission.
    /// </summary>
    /// <param name="command">The command containing the submission ID, form ID and new status code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// Success result with updated submission DTO if successful.
    /// NotFound if submission doesn't exist, doesn't match form ID, or its form is deleted.
    /// Invalid if status code is invalid or transition not allowed.
    /// Persistence failures are not caught here - they surface as a logged, opaque 500.
    /// </returns>
    public async Task<Result<SubmissionDto>> Handle(
        UpdateStatusCommand command,
        CancellationToken cancellationToken)
    {
        var submission = await submissionRepository.SingleOrDefaultAsync(
            new SubmissionByFormIdAndSubmissionIdSpec(command.FormId, command.SubmissionId),
            cancellationToken);
        if (submission == null)
        {
            return Result<SubmissionDto>.NotFound("Submission not found");
        }

        var rejected = ChangeStatus(submission, command);
        if (rejected is not null)
        {
            return rejected;
        }

        // After ChangeStatus and its catches: a persistence failure is not a rejected transition, and mapping
        // one to a 400 would report an EF Core tracking conflict as caller error. Let it reach
        // EndatixExceptionHandler as an opaque, logged 500.
        await submissionRepository.UpdateAsync(submission, cancellationToken);

        return Result<SubmissionDto>.Success(SubmissionDto.FromSubmission(submission));
    }

    /// <summary><see langword="null"/> when the status changed, else why it was rejected.</summary>
    private Result<SubmissionDto>? ChangeStatus(Submission submission, UpdateStatusCommand command)
    {
        try
        {
            submission.UpdateStatus(SubmissionStatus.FromCode(command.StatusCode));
            return null;
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid submission status code {StatusCode}", command.StatusCode);
            return Result<SubmissionDto>.Invalid(new ValidationError("Invalid status code."));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Submission status transition rejected for submission {SubmissionId}", command.SubmissionId);
            return Result<SubmissionDto>.Invalid(new ValidationError("Status transition is not allowed."));
        }
    }
}
