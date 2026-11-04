using System.Collections.Frozen;

namespace Endatix.Core.Entities;

/// <summary>One lifecycle event a step will raise: its kind, the status before it, and the revision it takes.</summary>
internal sealed record LifecycleChange(SubmissionLifecycleEventKind Kind, CollectionStatus? Previous, long Revision);

/// <summary>A planned lifecycle change: the status to store and the events to raise, revisions assigned.</summary>
internal sealed record LifecycleStep(CollectionStatus Status, IReadOnlyList<LifecycleChange> Changes)
{
    public bool IsNone => Changes.Count == 0;

    public static LifecycleStep None(CollectionStatus current) => new(current, []);
}

/// <summary>
/// The submission lifecycle rules. Pure: it reads the submission and returns a step, and never mutates it. It owns
/// which phase pair raises which event, which moves are refused, the revision each event takes, and how a drifted row
/// (status <c>complete</c> without <see cref="Submission.IsComplete"/>) is read.
/// </summary>
internal static class SubmissionLifecycle
{
    /// <summary>
    /// The events of a new submission: <c>created</c> carrying <paramref name="saved"/>, then, when the submission was
    /// saved in an outcome (completed, closed or lapsed), that outcome's event from <paramref name="entry"/> at the next
    /// revision.
    /// </summary>
    public static LifecycleStep Begin(Submission submission, CollectionStatus entry, CollectionStatus saved)
    {
        LifecycleChange created = new(SubmissionLifecycleEventKind.Created, null, submission.Revision + 1);
        if (saved.Phase is CollectionPhase.Pending or CollectionPhase.Active)
        {
            return new LifecycleStep(saved, [created]);
        }

        var kind = Decide(entry.Phase, saved.Phase)
            ?? throw new InvalidOperationException($"A submission can't be created as {saved.Code}.");
        LifecycleChange outcome = new(kind, entry, submission.Revision + 2);
        return new LifecycleStep(saved, [created, outcome]);
    }

    /// <summary>
    /// The step that moves the submission to <paramref name="next"/>: one event at the next revision. Moving to the
    /// status it is in is no step.
    /// </summary>
    /// <exception cref="InvalidOperationException">The move is refused; nothing has changed yet.</exception>
    public static LifecycleStep Move(Submission submission, CollectionStatus next)
    {
        var from = Effective(submission);
        if (from.Code == next.Code)
        {
            return LifecycleStep.None(submission.CollectionStatus);
        }

        var kind = Decide(from.Phase, next.Phase)
            ?? throw new InvalidOperationException(
                $"A submission that is {from.Phase.ToWireName()} ({from.Code}) can't move to {next.Code}.");
        return new LifecycleStep(next, [new LifecycleChange(kind, submission.CollectionStatus, submission.Revision + 1)]);
    }

    /// <summary>
    /// The status an in-progress save leaves the submission in: an active status stays as it is (a host's own active
    /// code included); pending and lapsed become <c>in_progress</c>.
    /// </summary>
    public static CollectionStatus SavedInProgress(Submission submission)
    {
        var current = Effective(submission);
        return current.Phase == CollectionPhase.Active ? current : CollectionStatus.InProgress;
    }

    /// <summary>Whether answers may still change and the status may still move: pending, active or lapsed.</summary>
    public static bool CanResume(Submission submission) => CanResume(Effective(submission).Phase);

    /// <summary>The status the rules read: <c>complete</c> without completion reads as active, completion as complete.</summary>
    public static CollectionStatus Effective(Submission submission)
    {
        if (submission.IsComplete)
        {
            return CollectionStatus.Complete;
        }

        return submission.CollectionStatus.Code == CollectionStatusCodes.Complete
            ? CollectionStatus.InProgress
            : submission.CollectionStatus;
    }

    /// <summary>The event a move between two phases raises, or null when the move is refused.</summary>
    internal static SubmissionLifecycleEventKind? Decide(CollectionPhase from, CollectionPhase to) =>
        _table.TryGetValue((from, to), out var kind) ? kind : null;

    private const SubmissionLifecycleEventKind Moved = SubmissionLifecycleEventKind.CollectionStatusMoved;
    private const SubmissionLifecycleEventKind Started = SubmissionLifecycleEventKind.Started;
    private const SubmissionLifecycleEventKind Completed = SubmissionLifecycleEventKind.Completed;
    private const SubmissionLifecycleEventKind Closed = SubmissionLifecycleEventKind.Closed;
    private const SubmissionLifecycleEventKind Lapsed = SubmissionLifecycleEventKind.Lapsed;
    private const SubmissionLifecycleEventKind Resumed = SubmissionLifecycleEventKind.Resumed;

    // The whole policy: rows from, columns to (pending, active, completed, closed, lapsed); null refuses the move.
    // Completed and closed are final, so their rows refuse everything.
    private static readonly SubmissionLifecycleEventKind?[,] _cells =
    {
        /* pending   */ { Moved, Started, Completed, Closed, Lapsed },
        /* active    */ { null, Moved, Completed, Closed, Lapsed },
        /* completed */ { null, null, null, null, null },
        /* closed    */ { null, null, null, null, null },
        /* lapsed    */ { null, Resumed, Completed, Closed, Moved },
    };

    private static readonly FrozenDictionary<(CollectionPhase From, CollectionPhase To), SubmissionLifecycleEventKind> _table =
        Enum.GetValues<CollectionPhase>()
            .SelectMany(from => Enum.GetValues<CollectionPhase>().Select(to => (From: from, To: to)))
            .Where(move => _cells[(int)move.From, (int)move.To] is not null)
            .ToFrozenDictionary(move => move, move => _cells[(int)move.From, (int)move.To]!.Value);

    private static bool CanResume(CollectionPhase phase) =>
        phase is CollectionPhase.Pending or CollectionPhase.Active or CollectionPhase.Lapsed;
}
