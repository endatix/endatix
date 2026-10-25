using Endatix.Core.Abstractions.Submissions;

namespace Endatix.Infrastructure.Features.Submissions;

internal sealed class OpenSubmissionAccessTokenGate : ISubmissionAccessTokenGate
{
    public Task<bool> IsCurrentAsync(SubmissionAccessTokenClaims claims, CancellationToken cancellationToken) =>
        Task.FromResult(true);
}
