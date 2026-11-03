using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Common;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using CollectionStatusValue = Endatix.Core.Entities.CollectionStatus;

namespace Endatix.Core.Entities;

public sealed partial class Submission : TenantEntity, IAggregateRoot, IOwnedEntity, IHasRevision
{
    private const string SINGLE_SUBMISSION_RESTRICTION_PREFIX = "SingleSubmission";

    // While true, collection status changes raise nothing: submission.created carries the initial status.
    private bool _constructing;

    private Submission() { } // For EF Core

    private Submission(SubmissionCreateArgs args) : base(args.TenantId)
    {
        Guard.Against.Null(args);
        Guard.Against.NullOrEmpty(args.JsonData);
        Guard.Against.NegativeOrZero(args.FormId);
        Guard.Against.NegativeOrZero(args.FormDefinitionId);

        _constructing = true;
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
        SetInitialCollectionStatus(args.IsComplete, args.StartSubmission);
        _constructing = false;
        RaiseCreated();
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

    /// <summary>
    /// One revision for the new submission: <c>submission.created</c>, and <c>submission.completed</c> when it was
    /// created complete.
    /// </summary>
    private void RaiseCreated()
    {
        RegisterRevisedDomainEvent(() => new SubmissionCreatedEvent(this));
        if (IsComplete)
        {
            RegisterDomainEvent(new SubmissionCompletedEvent(this));
        }
    }

    public void FreezePersonalization(long audienceLinkId, string snapshot)
    {
        Guard.Against.NegativeOrZero(audienceLinkId);
        Guard.Against.NullOrEmpty(snapshot);
        if (PersonalizationSnapshot is not null)
        {
            return;
        }

        AudienceLinkId = audienceLinkId;
        PersonalizationSnapshot = snapshot;
    }

    /// <summary>
    /// Freezes audience values captured for a trusted on-behalf create. No link is involved.
    /// </summary>
    public void FreezeOnBehalf(string snapshot)
    {
        Guard.Against.NullOrEmpty(snapshot);
        if (PersonalizationSnapshot is not null)
        {
            return;
        }

        PersonalizationSnapshot = snapshot;
    }

    /// <summary>
    /// Freezes the values a test submission is opened with. Unlike a link or an on-behalf create, a second freeze is
    /// a bug, not a retry, so it throws rather than keeping the first.
    /// </summary>
    /// <exception cref="InvalidOperationException">Not a test submission, or its values are already frozen.</exception>
    public void FreezeForTest(string snapshot)
    {
        Guard.Against.NullOrEmpty(snapshot);
        if (!IsTestSubmission)
        {
            throw new InvalidOperationException("Only a test submission takes test personalization.");
        }

        if (PersonalizationSnapshot is not null)
        {
            throw new InvalidOperationException("The test submission's personalization is already frozen.");
        }

        PersonalizationSnapshot = snapshot;
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

    /// <summary>Frozen audience values from the first open of a personalised link.</summary>
    public string? PersonalizationSnapshot { get; private set; }

    public long? AudienceLinkId { get; private set; }
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

    /// <summary>Updates submission content; the target definition must belong to this submission's form (<see cref="FormId"/>).</summary>
    public void Update(string jsonData, long formDefinitionId, long formDefinitionFormId, bool isComplete = true, int currentPage = 1, string? metadata = null)
    {
        Guard.Against.NullOrEmpty(jsonData);
        Guard.Against.NegativeOrZero(formDefinitionId);
        Guard.Against.NegativeOrZero(formDefinitionFormId);

        EnsureUpdateAllowed(formDefinitionFormId, isComplete);
        if (IsComplete || !isComplete)
        {
            // A save that completes records its start inside the one transition to complete.
            EnsureStarted();
        }

        SubmissionContent content = new(jsonData, formDefinitionId, currentPage, metadata);
        var changeKind = IsComplete ? ChangesTo(content) : SubmissionChangeKinds.None;
        ApplyContent(content);
        SetCompletionStatus(isComplete);

        if (changeKind != SubmissionChangeKinds.None)
        {
            RegisterRevisedDomainEvent(() => new SubmissionUpdatedEvent(this, changeKind));
        }
    }

    /// <summary>The answers, definition, page and metadata one save writes.</summary>
    private readonly record struct SubmissionContent(string JsonData, long FormDefinitionId, int CurrentPage, string? Metadata);

    private SubmissionChangeKinds ChangesTo(SubmissionContent content)
    {
        var changeKind = SubmissionChangeKinds.None;
        if (!string.Equals(JsonData, content.JsonData, StringComparison.Ordinal))
        {
            changeKind |= SubmissionChangeKinds.Answers;
        }

        if (!string.Equals(Metadata, content.Metadata, StringComparison.Ordinal))
        {
            changeKind |= SubmissionChangeKinds.Metadata;
        }

        return FormDefinitionId != content.FormDefinitionId
            ? changeKind | SubmissionChangeKinds.Definition
            : changeKind;
    }

    private void ApplyContent(SubmissionContent content)
    {
        FormDefinitionId = content.FormDefinitionId;
        JsonData = content.JsonData;
        CurrentPage = content.CurrentPage;
        Metadata = content.Metadata;
    }

    public void UpdateToken(Token token)
    {
        Token = token;
    }

    public void UpdateStatus(SubmissionStatus newStatus)
    {
        Guard.Against.Null(newStatus);

        if (Status == newStatus)
        {
            return;
        }

        var previousStatus = Status;
        // Clone so callers can pass catalog statics without sharing OwnsOne identity across aggregates
        Status = newStatus.CreateInstance();

        RegisterRevisedDomainEvent(() => new SubmissionStatusChangedEvent(this, previousStatus));
    }

    /// <summary>
    /// Flags the submission as test and clears its one-per-person key, so the person may answer for real.
    /// Idempotent.
    /// </summary>
    public void MarkAsTest()
    {
        if (IsTestSubmission)
        {
            return;
        }

        IsTestSubmission = true;
        ApplySingleSubmissionRestriction(FormId, shouldEnforce: false);
        RegisterRevisedDomainEvent(() => new SubmissionUpdatedEvent(this, SubmissionChangeKinds.Test));
    }

    /// <summary>
    /// Flags the submission as real. When the form allows one response per person, the key is re-applied and
    /// the unique index refuses the save if the person already has a real one (<c>DuplicateSubmissionException</c>).
    /// Idempotent.
    /// </summary>
    public void MarkAsReal(bool enforceSingleSubmissionGate)
    {
        if (!IsTestSubmission)
        {
            return;
        }

        IsTestSubmission = false;
        ApplySingleSubmissionRestriction(FormId, enforceSingleSubmissionGate);
        RegisterRevisedDomainEvent(() => new SubmissionUpdatedEvent(this, SubmissionChangeKinds.Test));
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
