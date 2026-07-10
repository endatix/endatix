using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;

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
        Status = SubmissionStatus.New;

        SetSubmitter(args.SubmitterId, args.SubmitterDisplayId, args.SubmitterProfileSnapshot);
        ApplySingleSubmissionRestriction(args.FormId, args.EnforceSingleSubmissionGate && !args.IsTestSubmission);
        SetCompletionStatus(args.IsComplete);
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

    public static Submission Create(SubmissionCreateArgs args)
    {
        Guard.Against.Null(args);
        return new Submission(args);
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
    public DateTime? CompletedAt { get; private set; }
    public Token? Token { get; private set; }
    public SubmissionStatus Status { get; private set; } = null!;

    /// <summary>Updates submission content; the target definition must belong to this submission's form (<see cref="FormId"/>).</summary>
    public void Update(string jsonData, long formDefinitionId, long formDefinitionFormId, bool isComplete = true, int currentPage = 1, string? metadata = null)
    {
        Guard.Against.NullOrEmpty(jsonData);
        Guard.Against.NegativeOrZero(formDefinitionId);
        Guard.Against.NegativeOrZero(formDefinitionFormId);

        if (formDefinitionFormId != FormId)
        {
            throw new ArgumentException(
                "The target form definition does not belong to this submission's form", nameof(formDefinitionFormId));
        }

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
        Status = newStatus;

        RegisterRevisedDomainEvent(() => new SubmissionStatusChangedEvent(this, previousStatus));
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
            new(id, EmptyToNull(displayId), EmptyToNull(profileSnapshot));

        public string? SubmittedBy => Id?.ToString() ?? DisplayId;

        private static string? EmptyToNull(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private void SetCompletionStatus(bool newIsCompleteValue)
    {
        if (!IsComplete && newIsCompleteValue)
        {
            IsComplete = true;
            CompletedAt = DateTime.UtcNow;
            // false→true transition (ctor or Update); captured to outbox → submission.completed webhook
            RegisterRevisedDomainEvent(() => new SubmissionCompletedEvent(this));
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
