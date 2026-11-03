using Endatix.Core.Events;
using CollectionStatusValue = Endatix.Core.Entities.CollectionStatus;

namespace Endatix.Core.Entities;

/// <summary>
/// The submission's collection status lifecycle. Every change goes through <see cref="TransitionTo"/>, which raises
/// <c>submission.collection_status_changed</c> once with one revision bump.
/// </summary>
public sealed partial class Submission
{
    /// <summary>
    /// Records first engagement once. Subsequent calls do not move <see cref="StartedAt"/>.
    /// An unengaged row (<c>not_started</c> / <c>viewed</c>) moves to <c>in_progress</c>, so a
    /// recorded start never sits next to an unengaged collection status. Terminal and custom
    /// statuses are left alone.
    /// </summary>
    public void EnsureStarted(DateTime? at = null)
    {
        if (IsUnengagedCollection())
        {
            TransitionTo(CollectionStatusValue.InProgress);
        }

        StartedAt ??= at ?? DateTime.UtcNow;
    }

    /// <summary>Owner or system void. Does not mark the interview complete.</summary>
    /// <exception cref="InvalidOperationException">The submission is already complete.</exception>
    public void Cancel()
    {
        // A complete row stays complete: cancelled with IsComplete true would contradict the dual-write.
        if (IsComplete)
        {
            throw new InvalidOperationException("A complete submission cannot be cancelled.");
        }

        // Screen-out is a final outcome; cancelling would silently lose it.
        if (IsScreenedOut)
        {
            throw new InvalidOperationException("A screened-out submission cannot be cancelled.");
        }

        TransitionTo(CollectionStatusValue.Cancelled);
    }

    /// <summary>True when the interview ended as screened out. A screened-out submission can't be changed.</summary>
    public bool IsScreenedOut => CollectionStatus?.Code == CollectionStatusCodes.ScreenOut;

    /// <summary>
    /// Saves the respondent's last answers and ends the interview as screened out, checking first so a
    /// rejected screen-out leaves the aggregate untouched. A repeat on a screened-out submission is a no-op.
    /// </summary>
    /// <exception cref="InvalidOperationException">The submission is complete, or its collection status is not resumable.</exception>
    public void ScreenOut(string jsonData, long formDefinitionId, long formDefinitionFormId, int currentPage = 1, string? metadata = null)
    {
        EnsureScreenOutAllowed();
        if (IsScreenedOut)
        {
            return;
        }

        Update(jsonData, formDefinitionId, formDefinitionFormId, isComplete: false, currentPage, metadata);
        ScreenOut();
    }

    /// <summary>
    /// Ends the interview as screened out. Does not mark it complete and does not raise
    /// <c>submission.completed</c>. Records the start if none was recorded. A later update is rejected.
    /// A repeat on a screened-out submission is a no-op.
    /// </summary>
    /// <exception cref="InvalidOperationException">The submission is complete, or its collection status is not resumable.</exception>
    public void ScreenOut()
    {
        EnsureScreenOutAllowed();
        if (IsScreenedOut)
        {
            return;
        }

        // Records the start without an in_progress step, so an unengaged submission screens out in one transition.
        StartedAt ??= DateTime.UtcNow;
        TransitionTo(CollectionStatusValue.ScreenOut);
    }

    private void EnsureScreenOutAllowed()
    {
        if (IsComplete)
        {
            throw new InvalidOperationException("A complete submission cannot be screened out.");
        }

        // Another terminal outcome (e.g. cancelled) is not overwritten.
        if (!IsScreenedOut && !IsResumableCollection())
        {
            throw new InvalidOperationException(
                "Cannot screen out a submission whose collection status is not resumable.");
        }
    }

    private void SetInitialCollectionStatus(bool isComplete, bool startSubmission)
    {
        SetCompletionStatus(isComplete);
        if (startSubmission)
        {
            EnsureStarted();
        }
        else if (!isComplete)
        {
            // Created on behalf (prefill): no respondent engagement yet, StartedAt stays null.
            TransitionTo(CollectionStatusValue.NotStarted);
        }
    }

    private void SetCompletionStatus(bool newIsCompleteValue)
    {
        if (IsComplete)
        {
            return;
        }

        if (newIsCompleteValue)
        {
            MarkComplete();
        }
        else if (IsResumableCollection())
        {
            TransitionTo(CollectionStatusValue.InProgress);
        }
    }

    private void MarkComplete()
    {
        IsComplete = true;
        CompletedAt = DateTime.UtcNow;
        // Complete without a prior engagement save (e.g. complete-on-create): duration ≈ 0
        StartedAt ??= CompletedAt;
        TransitionTo(CollectionStatusValue.Complete);
        if (!_constructing)
        {
            // Rides on the transition's revision; captured to outbox → submission.completed webhook
            RegisterDomainEvent(new SubmissionCompletedEvent(this));
        }
    }

    private bool IsResumableCollection()
    {
        var code = CollectionStatus?.Code;
        return code is null
            || code == CollectionStatusCodes.NotStarted
            || code == CollectionStatusCodes.Viewed
            || code == CollectionStatusCodes.InProgress
            || code == CollectionStatusCodes.Expired;
    }

    private bool IsUnengagedCollection()
    {
        var code = CollectionStatus?.Code;
        return code == CollectionStatusCodes.NotStarted
            || code == CollectionStatusCodes.Viewed;
    }

    /// <summary>
    /// The one place a collection status changes. A real change raises <c>submission.collection_status_changed</c>
    /// once, with one revision bump. A repeat of the current status raises nothing.
    /// </summary>
    private void TransitionTo(CollectionStatus next)
    {
        var previous = CollectionStatus;
        if (previous?.Code == next.Code)
        {
            return;
        }

        CollectionStatus = next.CreateInstance();
        if (!_constructing)
        {
            RegisterRevisedDomainEvent(() => new SubmissionCollectionStatusChangedEvent(this, previous));
        }
    }
}
