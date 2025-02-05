using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.UpdateStatus;

public class UpdateSubmissionStatusCommandHandler(
    IRepository<Submission> submissionRepository
) : ICommandHandler<UpdateSubmissionStatusCommand, Result<UpdateSubmissionStatusResponse>>
{
    public async Task<Result<UpdateSubmissionStatusResponse>> Handle(
        UpdateSubmissionStatusCommand command,
        CancellationToken cancellationToken)
    {
        var submission = await submissionRepository
            .GetByIdAsync(command.SubmissionId, cancellationToken);

        if (submission == null || submission.FormId != command.FormId)
        {
            return Result<UpdateSubmissionStatusResponse>.NotFound("Submission not found");
        }

        try
        {
            var newStatus = SubmissionStatus.FromCode(command.StatusCode);
            submission.UpdateStatus(newStatus);

            await submissionRepository.UpdateAsync(submission, cancellationToken);

            return Result<UpdateSubmissionStatusResponse>.Success(new UpdateSubmissionStatusResponse(
                submission.Id,
                newStatus.Name,
                DateTime.UtcNow
            ));
        }
        catch (ArgumentException ex)
        {
            return Result<UpdateSubmissionStatusResponse>.Invalid(new ValidationError($"Invalid status: {ex.Message}"));
        }
        catch (InvalidOperationException ex)
        {
            return Result<UpdateSubmissionStatusResponse>.Invalid(new ValidationError(ex.Message));
        }
    }
}