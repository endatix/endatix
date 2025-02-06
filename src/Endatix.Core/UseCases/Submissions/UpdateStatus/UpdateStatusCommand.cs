using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.UpdateStatus;

/// <summary>
/// Command to update the status of a form submission.
/// </summary>
/// <param name="SubmissionId">The unique identifier of the submission to update.</param>
/// <param name="FormId">The unique identifier of the form associated with the submission.</param>
/// <param name="StatusCode">The new status code to set for the submission.</param>
public record UpdateStatusCommand(
    long SubmissionId,
    long FormId,
    string StatusCode) : ICommand<Result<SubmissionDto>>;