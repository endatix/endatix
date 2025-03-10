using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

public partial class Submission : TenantEntity, IAggregateRoot
{
    private Submission() { } // For EF Core

    public Submission(long tenantId, string jsonData, long formId, long formDefinitionId, bool isComplete = true, int currentPage = 1, string? metadata = null)
        : base(tenantId)
    {
        Guard.Against.NullOrEmpty(jsonData, nameof(jsonData));
        Guard.Against.NegativeOrZero(formId, nameof(formId));
        Guard.Against.NegativeOrZero(formDefinitionId, nameof(formDefinitionId));

        FormId = formId;
        FormDefinitionId = formDefinitionId;
        JsonData = jsonData;
        CurrentPage = currentPage;
        Metadata = metadata;
        Status = SubmissionStatus.New;

        SetCompletionStatus(isComplete);
    }

    public bool IsComplete { get; private set; }
    public string JsonData { get; private set; }
    public FormDefinition FormDefinition { get; private set; }
    public long FormId { get; init; }
    public long FormDefinitionId { get; private set; }
    public int? CurrentPage { get; private set; }
    public string? Metadata { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Token? Token { get; private set; }
    public SubmissionStatus Status { get; private set; }

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
}
