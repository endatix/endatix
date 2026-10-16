using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Abstractions.Repositories;

/// <summary>
/// Saves of tracked submissions that report, rather than throw, a submission deleted since it was read, on its own or
/// with its form. Such a save rolled back, its outbox rows with it.
/// </summary>
public static class SubmissionRepositoryExtensions
{
    /// <returns><see langword="false"/> when a submission was deleted meanwhile.</returns>
    public static Task<bool> TrySaveChangesAsync(
        this IRepository<Submission> submissions,
        CancellationToken cancellationToken) =>
        UnlessDeletedMeanwhileAsync(() => submissions.SaveChangesAsync(cancellationToken));

    /// <returns><see langword="false"/> when <paramref name="submission"/> was deleted meanwhile.</returns>
    internal static Task<bool> TryUpdateAsync(
        this IRepository<Submission> submissions,
        Submission submission,
        CancellationToken cancellationToken) =>
        UnlessDeletedMeanwhileAsync(() => submissions.UpdateAsync(submission, cancellationToken));

    /// <summary>Runs <paramref name="save"/>, a save of tracked submissions.</summary>
    /// <returns><see langword="false"/> when it found a submission deleted since it was read.</returns>
    internal static async Task<bool> UnlessDeletedMeanwhileAsync(Func<Task> save)
    {
        try
        {
            await save();
            return true;
        }
        catch (SubmissionDeletedException)
        {
            return false;
        }
    }
}
