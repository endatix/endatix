using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Saves of a tracked submission that report, rather than throw, a submission deleted since it was read, on its own
/// or with its form. The save then rolled back, its outbox rows with it.
/// </summary>
public static class SubmissionSaveExtensions
{
    /// <returns><see langword="false"/> when a submission was deleted meanwhile.</returns>
    public static async Task<bool> TrySaveChangesAsync(
        this IRepository<Submission> submissions,
        CancellationToken cancellationToken)
    {
        try
        {
            await submissions.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (SubmissionDeletedException)
        {
            return false;
        }
    }

    /// <returns><see langword="false"/> when <paramref name="submission"/> was deleted meanwhile.</returns>
    public static async Task<bool> TryUpdateAsync(
        this IRepository<Submission> submissions,
        Submission submission,
        CancellationToken cancellationToken)
    {
        try
        {
            await submissions.UpdateAsync(submission, cancellationToken);
            return true;
        }
        catch (SubmissionDeletedException)
        {
            return false;
        }
    }
}
