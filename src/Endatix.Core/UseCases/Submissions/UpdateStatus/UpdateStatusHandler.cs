using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
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
    /// NotFound if submission doesn't exist or doesn't match form ID.
    /// Invalid if status code is invalid or transition not allowed.
    /// Persistence failures are not caught here - they surface as a logged, opaque 500.
    /// </returns>
    public async Task<Result<SubmissionDto>> Handle(
        UpdateStatusCommand command,
        CancellationToken cancellationToken)
    {
        var submission = await submissionRepository
            .GetByIdAsync(command.SubmissionId, cancellationToken);

        if (submission == null || submission.FormId != command.FormId)
        {
            return Result<SubmissionDto>.NotFound("Submission not found");
        }

        try
        {
            var newStatus = SubmissionStatus.FromCode(command.StatusCode);
            submission.UpdateStatus(newStatus);
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

        // Outside the catches above: a persistence failure is not a rejected transition, and mapping
        // one to a 400 would report an EF Core tracking conflict as caller error. Let it reach
        // EndatixExceptionHandler as an opaque, logged 500.
        await submissionRepository.UpdateAsync(submission, cancellationToken);

        return Result<SubmissionDto>.Success(SubmissionDto.FromSubmission(submission));
    }
}