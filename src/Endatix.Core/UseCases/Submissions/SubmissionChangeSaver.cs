using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Saves a change to a submission's answers or state, with a version of the answers it replaced.
/// </summary>
internal sealed class SubmissionChangeSaver(IRepository<Submission> submissions, IRepository<SubmissionVersion> versions)
{
    /// <summary>
    /// <see langword="false"/> when the submission was deleted after it was read, on its own or with its form: the
    /// save rolled back, the version and any event with it.
    /// </summary>
    public Task<bool> TrySaveAsync(Submission submission, string originalJson, CancellationToken cancellationToken) =>
        SubmissionRepositoryExtensions.UnlessDeletedMeanwhileAsync(() => SaveAsync(submission, originalJson, cancellationToken));

    /// <summary>Adding the version saves it with the change, so either call can find the submission deleted.</summary>
    private async Task SaveAsync(Submission submission, string originalJson, CancellationToken cancellationToken)
    {
        if (!string.Equals(originalJson, submission.JsonData, StringComparison.Ordinal))
        {
            var effectiveTimestamp = submission.ModifiedAt ?? submission.CreatedAt;
            await versions.AddAsync(new SubmissionVersion(submission.Id, originalJson, effectiveTimestamp), cancellationToken);
        }

        await submissions.SaveChangesAsync(cancellationToken);
    }
}
