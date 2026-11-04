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

    /// <summary>
    /// The whole policy, rows from, columns to. Null is a refused move.
    /// <code>
    ///            Pending   Active    Completed  Closed   Lapsed
    /// Pending    moved     started   completed  closed   lapsed
    /// Active     refused   moved     completed  closed   lapsed
    /// Lapsed     refused   resumed   completed  closed   moved
    /// Completed  refused   refused   refused    refused  refused
    /// Closed     refused   refused   refused    refused  refused
    /// </code>
    /// </summary>
    internal static SubmissionLifecycleEventKind? Decide(CollectionPhase from, CollectionPhase to) => (from, to) switch
    {
        (CollectionPhase.Completed or CollectionPhase.Closed, _) => null,
        (_, CollectionPhase.Completed) => SubmissionLifecycleEventKind.Completed,
        (_, CollectionPhase.Closed) => SubmissionLifecycleEventKind.Closed,
        (CollectionPhase.Pending, CollectionPhase.Pending) => SubmissionLifecycleEventKind.CollectionStatusMoved,
        (CollectionPhase.Pending, CollectionPhase.Active) => SubmissionLifecycleEventKind.Started,
        (CollectionPhase.Active, CollectionPhase.Active) => SubmissionLifecycleEventKind.CollectionStatusMoved,
        (CollectionPhase.Lapsed, CollectionPhase.Active) => SubmissionLifecycleEventKind.Resumed,
        (CollectionPhase.Lapsed, CollectionPhase.Lapsed) => SubmissionLifecycleEventKind.CollectionStatusMoved,
        (_, CollectionPhase.Lapsed) => SubmissionLifecycleEventKind.Lapsed,
        _ => null,
    };

    private static bool CanResume(CollectionPhase phase) =>
        phase is CollectionPhase.Pending or CollectionPhase.Active or CollectionPhase.Lapsed;
}
