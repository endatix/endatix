using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Repositories;

/// <summary>
/// Saves of tracked submissions that report, rather than throw, the two ways a save can lose to a concurrent write.
/// Either way the save rolled back, its outbox rows with it.
/// </summary>
public static class SubmissionRepositoryExtensions
{
    internal const string DeletedMeanwhileMessage = "Submission not found.";

    internal const string BusyMessage = "The submission is busy; try again shortly.";

    /// <summary>
    /// Saves the context's tracked changes. A tracked save writes only the columns that changed, never the
    /// submission's form and definition keys, so it takes no lock on those rows that a form's deletion holds.
    /// </summary>
    /// <returns>
    /// Success; <see cref="ResultStatus.NotFound"/> when a submission was deleted since it was read, on its own or
    /// with its form; or <see cref="ResultStatus.Unavailable"/> when the save was a deadlock victim.
    /// </returns>
    public static Task<Result> TrySaveChangesAsync(
        this IRepository<Submission> submissions,
        CancellationToken cancellationToken) =>
        SaveAsync(() => submissions.SaveChangesAsync(cancellationToken));

    /// <summary>Runs <paramref name="save"/>, a save of tracked submissions, and reports as <see cref="TrySaveChangesAsync"/> does.</summary>
    internal static async Task<Result> SaveAsync(Func<Task> save)
    {
        try
        {
            await save();
            return Result.Success();
        }
        catch (SubmissionDeletedException)
        {
            return Result.NotFound(DeletedMeanwhileMessage);
        }
        catch (DeadlockVictimException)
        {
            return Result.Unavailable(BusyMessage);
        }
    }
}
