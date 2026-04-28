using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

public partial class Submission : TenantEntity, IAggregateRoot, IOwnedEntity
{
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
        SubmittedBy = options.SubmittedBy;
        IsTestSubmission = options.IsTestSubmission;
        Status = SubmissionStatus.New;

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
                SubmittedBy: submittedBy,
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
    public bool IsTestSubmission { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Token? Token { get; private set; }
    public SubmissionStatus Status { get; private set; } = null!;

    public void Update(string jsonData, long formDefinitionId, bool isComplete = true, int currentPage = 1, string? metadata = null)
    {
        Guard.Against.NullOrEmpty(jsonData, nameof(jsonData));
        Guard.Against.NegativeOrZero(formDefinitionId, nameof(formDefinitionId));

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

    private void SetCompletionStatus(bool newIsCompleteValue)
    {
        if (!IsComplete && newIsCompleteValue)
        {
            IsComplete = true;
            CompletedAt = DateTime.UtcNow;
        }
    }

    public string? OwnerId => SubmittedBy;

    public override void Delete()
    {
        base.Delete();
    }
}

public sealed record SubmissionCreateOptions(
    bool IsComplete = true,
    int CurrentPage = 0,
    string? Metadata = null,
    string? SubmittedBy = null,
    bool IsTestSubmission = false);
