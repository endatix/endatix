using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.CreateAccessToken;

/// <summary>
/// Command to generate a short-lived access token for a submission.
/// </summary>
/// <param name="FormId">The ID of the form</param>
/// <param name="SubmissionId">The ID of the submission</param>
/// <param name="ExpiryMinutes">Expiry time in minutes</param>
/// <param name="Permissions">Array of permissions (view, edit, export)</param>
public record CreateAccessTokenCommand(
    long FormId,
    long SubmissionId,
    int ExpiryMinutes,
    IEnumerable<string> Permissions) : ICommand<Result<SubmissionAccessTokenDto>>;
