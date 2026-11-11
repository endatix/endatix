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

        SetSubmitter(args.SubmitterId, args.SubmitterDisplayId, args.SubmitterProfileSnapshot);
        ApplySingleSubmissionRestriction(args.FormId, args.EnforceSingleSubmissionGate && !args.IsTestSubmission);
        Begin(args);
    }

    private Submission(long id, SubmissionCreateArgs args) : this(args)
    {
        Guard.Against.NegativeOrZero(id);
        Id = id;
    }

#pragma warning disable S1133 // Kept obsolete for callers-only / tombstone policy (Loop A #1472); removal is a separate decision.
    [Obsolete("Use Submission.Create(SubmissionCreateArgs).")]
#pragma warning restore S1133
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
    // EF navigation: materialised by the ORM; domain code never assigns it.
#pragma warning disable S1144
    public Submitter? Submitter { get; private set; }
#pragma warning restore S1144
    public string? SubmitterDisplayId { get; private set; }
    public string? SubmitterProfileSnapshot { get; private set; }

    /// <summary>Frozen audience values from the first open of a personalised link.</summary>
    public string? PersonalizationSnapshot { get; private set; }

    public long? AudienceLinkId { get; private set; }
    public bool IsTestSubmission { get; private set; }
    public string? RestrictionKey { get; private set; }

    /// <summary>
    /// The submission's event sequence: every revised event (each lifecycle event, <c>submission.updated</c>,
    /// <c>submission.status_changed</c>) raises it by exactly one and carries the new value. A new submission is at 2,
    /// or 3 when it was created complete or in an outcome.
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
    /// Saves content; the target definition must belong to this submission's form (<see cref="FormId"/>). A save that
    /// completes raises <c>submission.completed</c>. An in-progress save raises <c>submission.started</c> on a pending
    /// submission, <c>submission.resumed</c> on a lapsed one, and nothing on an active one. An edit of a complete
    /// submission raises <c>submission.updated</c> when it changes something. A closed submission can't be saved.
    /// </summary>
    /// <exception cref="ArgumentException">The definition belongs to another form.</exception>
    /// <exception cref="InvalidOperationException">The submission is closed.</exception>
    public void Update(string jsonData, long formDefinitionId, long formDefinitionFormId, bool isComplete = true, int currentPage = 1, string? metadata = null)
    {
        SubmissionContent content = new(jsonData, formDefinitionId, currentPage, metadata);
        EnsureContentAllowed(content, formDefinitionFormId);
        if (IsComplete)
        {
            var changeKind = ChangesTo(content);
            ApplyContent(content);
            if (changeKind != SubmissionChangeKinds.None)
            {
                RecordNext(() => new SubmissionUpdatedEvent(this, changeKind));
            }

            return;
        }

        var step = SubmissionLifecycle.Move(this, isComplete ? CollectionStatusValue.Complete : SubmissionLifecycle.SavedInProgress(this));
        ApplyContent(content);
        var now = DateTime.UtcNow;
        if (isComplete)
        {
            IsComplete = true;
            CompletedAt = now;
        }

        StartedAt ??= now;
        Apply(step);
    }

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

    /// <summary>Owner or system void. Same as <see cref="Close(CollectionStatus)"/> with <c>cancelled</c>.</summary>
    /// <exception cref="InvalidOperationException">The submission is complete or already closed in another outcome.</exception>
    public void Cancel() => Close(CollectionStatusValue.Cancelled);

    /// <summary>True when the interview ended as screened out.</summary>
    public bool IsScreenedOut => CollectionStatus?.Code == CollectionStatusCodes.ScreenOut;

    /// <summary>Saves the last answers and screens out. Same as <see cref="Close(CollectionStatus, SubmissionContent, long)"/> with <c>screen_out</c>.</summary>
    /// <exception cref="InvalidOperationException">The submission is complete or already closed in another outcome.</exception>
    public void ScreenOut(string jsonData, long formDefinitionId, long formDefinitionFormId, int currentPage = 1, string? metadata = null) =>
        Close(CollectionStatusValue.ScreenOut, new SubmissionContent(jsonData, formDefinitionId, currentPage, metadata), formDefinitionFormId);

    /// <summary>Ends the interview as screened out. Same as <see cref="Close(CollectionStatus)"/> with <c>screen_out</c>.</summary>
    /// <exception cref="InvalidOperationException">The submission is complete or already closed in another outcome.</exception>
    public void ScreenOut() => Close(CollectionStatusValue.ScreenOut);

    /// <summary>Sets the token a respondent resumes the submission with.</summary>
    public void UpdateToken(Token token)
    {
        Token = token;
    }

    /// <summary>Sets the reviewer status. A change raises <c>submission.status_changed</c> with one revision bump.</summary>
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

        RecordNext(() => new SubmissionStatusChangedEvent(this, previousStatus));
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
        RecordNext(() => new SubmissionUpdatedEvent(this, SubmissionChangeKinds.Test));
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
        RecordNext(() => new SubmissionUpdatedEvent(this, SubmissionChangeKinds.Test));
    }

    /// <summary>Not supported: only the submission's own events raise its revision.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
#pragma warning disable S1133 // Kept obsolete for callers-only / tombstone policy (Loop A #1472); removal is a separate decision.
    [Obsolete("Retired: a submission raises its own revision with each event it raises.", error: true)]
#pragma warning restore S1133
    public void IncrementRevision() =>
        throw new NotSupportedException("A submission raises its own revision with each event it raises.");

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
            RecordNext(() => new SubmissionUpdatedEvent(this, SubmissionChangeKinds.Submitter));
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
