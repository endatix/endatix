using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.CreateAccessToken;

/// <summary>
/// Command to generate a short-lived access token for a submission.
/// </summary>
public record CreateAccessTokenCommand : ICommand<Result<SubmissionAccessTokenDto>>
{
    public long FormId { get; init; }
    public long SubmissionId { get; init; }
    public int ExpiryMinutes { get; init; }
    public IEnumerable<string> Permissions { get; init; }

    public CreateAccessTokenCommand(
        long formId,
        long submissionId,
        int expiryMinutes,
        IEnumerable<string> permissions)
    {
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NegativeOrZero(submissionId);
        Guard.Against.NegativeOrZero(expiryMinutes);
        Guard.Against.NullOrEmpty(permissions);

        FormId = formId;
        SubmissionId = submissionId;
        ExpiryMinutes = expiryMinutes;
        Permissions = permissions;
    }
}
