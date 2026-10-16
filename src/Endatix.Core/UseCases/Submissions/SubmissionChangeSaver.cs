using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
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
    public async Task<bool> TrySaveAsync(Submission submission, string originalJson, CancellationToken cancellationToken)
    {
        try
        {
            // Adding the version saves it with the change, so the deleted-meanwhile refusal can come from either call.
            if (!string.Equals(originalJson, submission.JsonData, StringComparison.Ordinal))
            {
                var effectiveTimestamp = submission.ModifiedAt ?? submission.CreatedAt;
                await versions.AddAsync(new SubmissionVersion(submission.Id, originalJson, effectiveTimestamp), cancellationToken);
            }

            await submissions.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (SubmissionDeletedException)
        {
            return false;
        }
    }
}
