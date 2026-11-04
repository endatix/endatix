using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;
using CollectionStatusValue = Endatix.Core.Entities.CollectionStatus;

namespace Endatix.Core.Entities;

/// <summary>
/// The submission's collection lifecycle. Each mutator asks <see cref="SubmissionLifecycle"/> for a step and applies
/// it; <see cref="Apply"/> is the only writer of <see cref="CollectionStatus"/> after construction and
/// <see cref="Record"/> the only writer of <see cref="Revision"/>.
/// </summary>
public sealed partial class Submission
{
    /// <summary>
    /// Records first engagement once; later calls don't move <see cref="StartedAt"/>. A pending submission moves to
    /// <c>in_progress</c> and raises <c>submission.started</c>. Other phases are left alone.
    /// </summary>
    public void EnsureStarted(DateTime? at = null)
    {
        var step = CollectionStatus.Phase == CollectionPhase.Pending
            ? SubmissionLifecycle.Move(this, CollectionStatusValue.InProgress)
            : LifecycleStep.None(CollectionStatus);
        StartedAt ??= at ?? DateTime.UtcNow;
        Apply(step);
    }

    /// <summary>
    /// Ends collection in a closed-phase outcome, e.g. <c>screen_out</c>, <c>quota_full</c> or a host code, and
    /// raises <c>submission.closed</c>. Records the start if none was recorded. A repeat of the current outcome is a
    /// no-op.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> is not a closed-phase status.</exception>
    /// <exception cref="InvalidOperationException">The submission is complete or already closed in another outcome.</exception>
    public void Close(CollectionStatus outcome)
    {
        EnsureClosedOutcome(outcome);
        if (CollectionStatus.Code == outcome.Code)
        {
            return;
        }

        var step = SubmissionLifecycle.Move(this, outcome);
        StartedAt ??= DateTime.UtcNow;
        Apply(step);
    }

    /// <summary>
    /// Saves the respondent's last answers, then ends collection in a closed-phase outcome: one event, one revision.
    /// Checks everything first, so a refused close leaves the submission untouched. A repeat is a no-op.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="outcome"/> is not a closed-phase status, or the definition belongs to another form.
    /// </exception>
    /// <exception cref="InvalidOperationException">The submission is complete or already closed in another outcome.</exception>
    public void Close(CollectionStatus outcome, SubmissionContent content, long formDefinitionFormId)
    {
        EnsureClosedOutcome(outcome);
        if (CollectionStatus.Code == outcome.Code)
        {
            return;
        }

        EnsureContentAllowed(content, formDefinitionFormId);
        var step = SubmissionLifecycle.Move(this, outcome);
        ApplyContent(content);
        StartedAt ??= DateTime.UtcNow;
        Apply(step);
    }

    /// <summary>
    /// Sets the status a new submission is saved in and raises its events: <c>created</c>, then the outcome's event
    /// when it was created complete or in an outcome.
    /// </summary>
    private void Begin(SubmissionCreateArgs args)
    {
        var entry = args.StartSubmission ? CollectionStatusValue.InProgress : CollectionStatusValue.NotStarted;
        var saved = SavedStatus(args, entry);
        var now = DateTime.UtcNow;
        if (args.IsComplete)
        {
            IsComplete = true;
            CompletedAt = now;
        }

        if (saved != entry || args.StartSubmission)
        {
            StartedAt = now;
        }

        CollectionStatus = entry.CreateInstance();
        Apply(SubmissionLifecycle.Begin(this, entry, saved));
    }

    private static CollectionStatus SavedStatus(SubmissionCreateArgs args, CollectionStatus entry)
    {
        if (args.CollectionOutcome is not { } outcome)
        {
            return args.IsComplete ? CollectionStatusValue.Complete : entry;
        }

        EnsureClosedOutcome(outcome);
        return args.IsComplete
            ? throw new ArgumentException("A submission is created complete or in an outcome, not both.", nameof(args))
            : outcome;
    }

    private static void EnsureClosedOutcome(CollectionStatus outcome)
    {
        Guard.Against.Null(outcome);
        if (outcome.Phase != CollectionPhase.Closed)
        {
            throw new ArgumentException($"'{outcome.Code}' is not a closed collection status.", nameof(outcome));
        }
    }

    /// <summary>The guard every content save shares: valid content, a definition of this form, an open submission.</summary>
    private void EnsureContentAllowed(SubmissionContent content, long formDefinitionFormId)
    {
        Guard.Against.NullOrEmpty(content.JsonData, nameof(content.JsonData));
        Guard.Against.NegativeOrZero(content.FormDefinitionId, nameof(content.FormDefinitionId));
        Guard.Against.NegativeOrZero(formDefinitionFormId);
        if (formDefinitionFormId != FormId)
        {
            throw new ArgumentException(
                "The target form definition does not belong to this submission's form", nameof(formDefinitionFormId));
        }

        if (SubmissionLifecycle.Effective(this).Phase == CollectionPhase.Closed)
        {
            throw new InvalidOperationException(IsScreenedOut
                ? "A screened-out submission cannot be changed."
                : $"A closed submission ({CollectionStatus.Code}) cannot be changed.");
        }
    }

    /// <summary>Stores the step's status and raises its events. No events, no change.</summary>
    private void Apply(LifecycleStep step)
    {
        if (step.IsNone)
        {
            return;
        }

        CollectionStatus = step.Status.CreateInstance();
        foreach (var change in step.Changes)
        {
            Record(change.Revision, () => new SubmissionLifecycleEvent(this, step.Status, change));
        }
    }

    /// <summary>Raises a revised event at the next revision: <c>updated</c>, <c>status_changed</c>.</summary>
    private void RecordNext(Func<DomainEventBase> raise) => Record(Revision + 1, raise);

    /// <summary>
    /// The only writer of <see cref="Revision"/>. Takes the event's revision, which must be the next one, so one event is
    /// one bump and the revision only rises; then raises the event, which reads the new revision.
    /// </summary>
    private void Record(long revision, Func<DomainEventBase> raise)
    {
        if (revision != Revision + 1)
        {
            throw new InvalidOperationException(
                $"An event must take revision {Revision + 1}, not {revision}.");
        }

        Revision = revision;
        RegisterDomainEvent(raise());
    }
}
