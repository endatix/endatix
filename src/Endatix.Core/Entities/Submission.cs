using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Common;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using CollectionStatusValue = Endatix.Core.Entities.CollectionStatus;

namespace Endatix.Core.Entities;

public sealed class Submission : TenantEntity, IAggregateRoot, IOwnedEntity, IHasRevision
{
    private const string SINGLE_SUBMISSION_RESTRICTION_PREFIX = "SingleSubmission";

    private Submission() { } // For EF Core

    private Submission(SubmissionCreateArgs args) : base(args.TenantId)
    {
        Guard.Against.Null(args);
        Guard.Against.NullOrEmpty(args.JsonData);
        Guard.Against.NegativeOrZero(args.FormId);
        Guard.Against.NegativeOrZero(args.FormDefinitionId);

        FormId = args.FormId;
        FormDefinitionId = args.FormDefinitionId;
        JsonData = args.JsonData;
        CurrentPage = args.CurrentPage;
        Metadata = args.Metadata;
        IsTestSubmission = args.IsTestSubmission;
        Status = SubmissionStatus.FromCode(SubmissionStatusCodes.New);
        CollectionStatus = CollectionStatusValue.InProgress.CreateInstance();

        SetSubmitter(args.SubmitterId, args.SubmitterDisplayId, args.SubmitterProfileSnapshot);
        ApplySingleSubmissionRestriction(args.FormId, args.EnforceSingleSubmissionGate && !args.IsTestSubmission);
        SetCompletionStatus(args.IsComplete);

        if (args.StartSubmission)
        {
            EnsureStarted();
        }
        else if (!args.IsComplete)
        {
            // Created on behalf (prefill): no respondent engagement yet, StartedAt stays null.
            SetCollectionStatus(CollectionStatusValue.NotStarted);
        }
    }

    private Submission(long id, SubmissionCreateArgs args) : this(args)
    {
        Guard.Against.NegativeOrZero(id);
        Id = id;
    }

    [Obsolete("Use Submission.Create(SubmissionCreateArgs).")]
    public Submission(
        long tenantId,
        string jsonData,
        long formId,
        long formDefinitionId,
        bool isComplete = true,
        int currentPage = 0,
        string? metadata = null,
        string? submittedBy = null,
        bool isTestSubmission = false,
        bool enforceSingleSubmissionGate = false)
        : this(new SubmissionCreateArgs(
            TenantId: tenantId,
            FormId: formId,
            FormDefinitionId: formDefinitionId,
            JsonData: jsonData,
            IsComplete: isComplete,
            CurrentPage: currentPage,
            Metadata: metadata,
            SubmitterId: submittedBy is not null && long.TryParse(submittedBy, out var legacySubmitterId)
                ? legacySubmitterId
                : null,
            SubmitterDisplayId: submittedBy is not null && !long.TryParse(submittedBy, out _)
                ? submittedBy
                : null,
            IsTestSubmission: isTestSubmission,
            EnforceSingleSubmissionGate: enforceSingleSubmissionGate))
    {
    }

    /// <summary>Normal path. Id stays 0 until EF OnAdd stamps the snowflake.</summary>
    public static Submission Create(SubmissionCreateArgs args)
    {
        Guard.Against.Null(args);
        return new Submission(args);
    }

    /// <summary>Explicit Id for tests, imports, seeding. Must be positive.</summary>
    public static Submission Create(long id, SubmissionCreateArgs args)
    {
        Guard.Against.Null(args);
        return new Submission(id, args);
    }

    public bool IsComplete { get; private set; }
    public string JsonData { get; private set; } = null!;
    public FormDefinition FormDefinition { get; private set; } = null!;
    public Form Form { get; private set; } = null!;
    public long FormId { get; init; }
    public long FormDefinitionId { get; private set; }
    public int? CurrentPage { get; private set; }
    public string? Metadata { get; private set; }
    public string? SubmittedBy { get; private set; }
    public long? SubmitterId { get; private set; }
    public Submitter? Submitter { get; private set; }
    public string? SubmitterDisplayId { get; private set; }
    public string? SubmitterProfileSnapshot { get; private set; }
    public bool IsTestSubmission { get; private set; }
    public string? RestrictionKey { get; private set; }

    /// <summary>
    /// Monotonic aggregate revision, bumped on each business mutation (update, status change,
    /// completion). Carried in integration event payloads so an order-sensitive consumer (e.g. a
    /// future audit log) can reconstruct order or detect gaps. Increment wiring lands with the
    /// event-raising work (Phase 5).
    /// </summary>
    public long Revision { get; private set; } = 1;

    /// <summary>
    /// First respondent engagement timestamp. Null until recorded via respondent create
    /// (<see cref="SubmissionCreateArgs.StartSubmission"/>), the first content
    /// <see cref="Update"/>, or completion without a prior start. Distinct from
    /// <see cref="BaseEntity.CreatedAt"/>, which is when the submission row was created
    /// (e.g. prefill / create-on-behalf).
    /// </summary>
    public DateTime? StartedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }
    public Token? Token { get; private set; }
    public SubmissionStatus Status { get; private set; } = null!;
    public CollectionStatus CollectionStatus { get; private set; } =
        CollectionStatusValue.InProgress;

    /// <summary>True when <see cref="StartedAt"/> has been recorded.</summary>
    public bool HasStarted => StartedAt is not null;

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
            SetCollectionStatus(CollectionStatusValue.InProgress);
        }

        if (HasStarted)
        {
            return;
        }

        StartedAt = at ?? DateTime.UtcNow;
    }

    /// <summary>Updates submission content; the target definition must belong to this submission's form (<see cref="FormId"/>).</summary>
    public void Update(string jsonData, long formDefinitionId, long formDefinitionFormId, bool isComplete = true, int currentPage = 1, string? metadata = null)
    {
        Guard.Against.NullOrEmpty(jsonData);
        Guard.Against.NegativeOrZero(formDefinitionId);
        Guard.Against.NegativeOrZero(formDefinitionFormId);

        EnsureUpdateAllowed(formDefinitionFormId, isComplete);
        EnsureStarted();

        var changeKind = SubmissionChangeKinds.None;
        if (IsComplete)
        {
            if (!string.Equals(JsonData, jsonData, StringComparison.Ordinal))
            {
                changeKind |= SubmissionChangeKinds.Answers;
            }

            if (!string.Equals(Metadata, metadata, StringComparison.Ordinal))
            {
                changeKind |= SubmissionChangeKinds.Metadata;
            }

            if (FormDefinitionId != formDefinitionId)
            {
                changeKind |= SubmissionChangeKinds.Definition;
            }
        }

        FormDefinitionId = formDefinitionId;
        JsonData = jsonData;
        CurrentPage = currentPage;
        Metadata = metadata;

        SetCompletionStatus(isComplete);

        if (changeKind != SubmissionChangeKinds.None)
        {
            RegisterRevisedDomainEvent(() => new SubmissionUpdatedEvent(this, changeKind));
        }
    }

    public void UpdateToken(Token token)
    {
        Token = token;
    }

    public void UpdateStatus(SubmissionStatus newStatus)
    {
        Guard.Against.Null(newStatus, nameof(newStatus));

        if (Status == newStatus)
        {
            return;
        }

        var previousStatus = Status;
        // Clone so callers can pass catalog statics without sharing OwnsOne identity across aggregates
        Status = newStatus.CreateInstance();

        RegisterRevisedDomainEvent(() => new SubmissionStatusChangedEvent(this, previousStatus));
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

        SetCollectionStatus(CollectionStatusValue.Cancelled);
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

        var previousCollectionStatus = CollectionStatus;
        EnsureStarted();
        SetCollectionStatus(CollectionStatusValue.ScreenOut);
        RegisterRevisedDomainEvent(() => new SubmissionCollectionStatusChangedEvent(this, previousCollectionStatus));
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

    /// <summary>Advances the aggregate revision. Call from domain mutations that raise integration events.</summary>
    public void IncrementRevision() => Revision++;

    private void RegisterRevisedDomainEvent(Func<DomainEventBase> eventFactory)
    {
        IncrementRevision();
        RegisterDomainEvent(eventFactory());
    }

    /// <summary>
    /// Sets submitter identity on the submission. Raises <c>submission.updated</c> with
    /// <see cref="SubmissionChangeKinds.Submitter"/> when the submission is complete and any
    /// identity field changes.
    /// </summary>
    public void SetSubmitter(long? submitterId, string? displayId, string? profileSnapshot)
    {
        var incoming = SubmitterIdentity.From(submitterId, displayId, profileSnapshot);
        SubmitterIdentity current = new(SubmitterId, SubmitterDisplayId, SubmitterProfileSnapshot);
        var submitterChangedOnCompleteSubmission = IsComplete && incoming != current;

        SubmitterId = incoming.Id;
        SubmittedBy = incoming.SubmittedBy;
        SubmitterDisplayId = incoming.DisplayId;
        SubmitterProfileSnapshot = incoming.ProfileSnapshot;

        if (submitterChangedOnCompleteSubmission)
        {
            RegisterRevisedDomainEvent(() => new SubmissionUpdatedEvent(this, SubmissionChangeKinds.Submitter));
        }
    }

    /// <summary>
    /// Optional submitter identity carried on a submission. Whitespace-only display id and profile
    /// snapshot are stored as null.
    /// </summary>
    private readonly record struct SubmitterIdentity(long? Id, string? DisplayId, string? ProfileSnapshot)
    {
        public static SubmitterIdentity From(long? id, string? displayId, string? profileSnapshot) =>
            new(id, displayId.NullIfWhiteSpace(), profileSnapshot.NullIfWhiteSpace());

        public string? SubmittedBy => Id?.ToString() ?? DisplayId;
    }

    private void EnsureUpdateAllowed(long formDefinitionFormId, bool isComplete)
    {
        if (formDefinitionFormId != FormId)
        {
            throw new ArgumentException(
                "The target form definition does not belong to this submission's form", nameof(formDefinitionFormId));
        }

        if (IsScreenedOut)
        {
            throw new InvalidOperationException("A screened-out submission cannot be changed.");
        }

        if (!IsComplete && isComplete && !IsResumableCollection())
        {
            throw new InvalidOperationException(
                "Cannot complete a submission whose collection status is not resumable.");
        }
    }

    private void SetCompletionStatus(bool newIsCompleteValue)
    {
        if (!IsComplete && newIsCompleteValue)
        {
            IsComplete = true;
            CompletedAt = DateTime.UtcNow;
            if (!HasStarted)
            {
                // Complete without a prior engagement save (e.g. complete-on-create): duration ≈ 0
                StartedAt = CompletedAt;
            }

            // false→true transition (ctor or Update); captured to outbox → submission.completed webhook
            RegisterRevisedDomainEvent(() => new SubmissionCompletedEvent(this));
            SetCollectionStatus(CollectionStatusValue.Complete);
            return;
        }

        if (!IsComplete && IsResumableCollection())
        {
            SetCollectionStatus(CollectionStatusValue.InProgress);
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

    private void SetCollectionStatus(CollectionStatus status) =>
        CollectionStatus = status.CreateInstance();

    public string? OwnerId => SubmitterId?.ToString() ?? SubmittedBy;

    public override void Delete()
    {
        base.Delete();
        RegisterDomainEvent(new SubmissionDeletedEvent(this));
    }

    private void ApplySingleSubmissionRestriction(long formId, bool shouldEnforce)
    {
        RestrictionKey = shouldEnforce && SubmitterId is not null
            ? $"{SINGLE_SUBMISSION_RESTRICTION_PREFIX}:Form:{formId}:Submitter:{SubmitterId}"
            : null;
    }
}
