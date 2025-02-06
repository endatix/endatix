using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.UpdateStatus;

/// <summary>
/// Handles updating the status of a submission.
/// </summary>
public class UpdateStatusHandler(
    IRepository<Submission> submissionRepository
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

            await submissionRepository.UpdateAsync(submission, cancellationToken);

            return Result<SubmissionDto>.Success(SubmissionDto.FromSubmission(submission));
        }
        catch (ArgumentException ex)
        {
            return Result<SubmissionDto>.Invalid(new ValidationError($"Invalid status code provided: {ex.Message}"));
        }
        catch (InvalidOperationException ex)
        {
            // not allowed status transition
            return Result<SubmissionDto>.Invalid(new ValidationError(ex.Message));
        }
    }
}