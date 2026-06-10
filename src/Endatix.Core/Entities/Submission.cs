using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

public partial class Submission : TenantEntity, IAggregateRoot, IOwnedEntity
{
    private const string SINGLE_SUBMISSION_RESTRICTION_PREFIX = "SingleSubmission";

    private Submission() { } // For EF Core

    public Submission(long tenantId, string jsonData, long formId, long formDefinitionId, SubmissionCreateOptions options)
        : base(tenantId)
    {
        Guard.Against.NullOrEmpty(jsonData, nameof(jsonData));
        Guard.Against.NegativeOrZero(formId, nameof(formId));
        Guard.Against.NegativeOrZero(formDefinitionId, nameof(formDefinitionId));
        Guard.Against.Null(options, nameof(options));

        FormId = formId;
        FormDefinitionId = formDefinitionId;
        JsonData = jsonData;
        CurrentPage = options.CurrentPage;
        Metadata = options.Metadata;
        IsTestSubmission = options.IsTestSubmission;
        Status = SubmissionStatus.New;

        SetSubmitter(options.SubmitterId, options.SubmitterDisplayId, options.SubmitterProfileSnapshot);
        ApplySingleSubmissionRestriction(formId, options.EnforceSingleSubmissionGate && !options.IsTestSubmission);
        SetCompletionStatus(options.IsComplete);
    }

    [Obsolete("Use the options-based Submission constructor or Submission.Create().")]
    public Submission(
        long tenantId,
        string jsonData,
        long formId,
        long formDefinitionId,
        bool isComplete = true,
        int currentPage = 0,
        string? metadata = null,
        string? submittedBy = null,
        bool isTestSubmission = false)
        : this(
            tenantId,
            jsonData,
            formId,
            formDefinitionId,
            new SubmissionCreateOptions(
                IsComplete: isComplete,
                CurrentPage: currentPage,
                Metadata: metadata,
                IsTestSubmission: isTestSubmission))
    {
    }

    public static Submission Create(
        long tenantId,
        string jsonData,
        long formId,
        long formDefinitionId,
        SubmissionCreateOptions? options = null)
    {
        return new Submission(tenantId, jsonData, formId, formDefinitionId, options ?? new SubmissionCreateOptions());
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

        FormDefinitionId = formDefinitionId;
        JsonData = jsonData;
        CurrentPage = currentPage;
        Metadata = metadata;

        SetCompletionStatus(isComplete);
    }

    public void UpdateToken(Token token)
    {
        Token = token;
    }

    public void UpdateStatus(SubmissionStatus newStatus)
    {
        Guard.Against.Null(newStatus, nameof(newStatus));

        Status = newStatus;
    }

    /// <summary>
    /// Sets the submitter for the submission.
    /// </summary>
    /// <param name="submitterId">The ID of the submitter.</param>
    /// <param name="displayId">The display ID of the submitter.</param>
    /// <param name="profileSnapshot">The profile snapshot of the submitter.</param>    
    public void SetSubmitter(long? submitterId, string? displayId, string? profileSnapshot)
    {
        SubmitterId = submitterId;
        SubmittedBy = submitterId?.ToString();
        SubmitterDisplayId = string.IsNullOrWhiteSpace(displayId) ? null : displayId;
        SubmitterProfileSnapshot = string.IsNullOrWhiteSpace(profileSnapshot) ? null : profileSnapshot;
    }

    private void SetCompletionStatus(bool newIsCompleteValue)
    {
        if (!IsComplete && newIsCompleteValue)
        {
            IsComplete = true;
            CompletedAt = DateTime.UtcNow;
        }
    }

    public string? OwnerId => SubmitterId?.ToString() ?? SubmittedBy;

    public override void Delete()
    {
        base.Delete();
    }

    private void ApplySingleSubmissionRestriction(long formId, bool shouldEnforce)
    {
        RestrictionKey = shouldEnforce && SubmitterId is not null
            ? $"{SINGLE_SUBMISSION_RESTRICTION_PREFIX}:Form:{formId}:Submitter:{SubmitterId}"
            : null;
    }
}

public sealed record SubmissionCreateOptions(
    bool IsComplete = true,
    int CurrentPage = 0,
    string? Metadata = null,
    long? SubmitterId = null,
    string? SubmitterDisplayId = null,
    string? SubmitterProfileSnapshot = null,
    bool IsTestSubmission = false,
    bool EnforceSingleSubmissionGate = false);
