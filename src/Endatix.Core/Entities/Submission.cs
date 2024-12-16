using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

public partial class Submission : BaseEntity, IAggregateRoot
{
    private Submission() { }

    public Submission(string jsonData, long formId, long formDefinitionId, bool isComplete = true, int currentPage = 1, string? metadata = null)
    {
        Guard.Against.NegativeOrZero(formId, nameof(formId));
        Guard.Against.NegativeOrZero(formDefinitionId, nameof(formDefinitionId));

        FormId = formId;
        FormDefinitionId = formDefinitionId;
        JsonData = jsonData;
        CurrentPage = currentPage;
        Metadata = metadata;

        SetCompletionStatus(isComplete);
    }

    public Submission(long id, string jsonData, long formId, long formDefinitionId, bool isComplete, int currentPage, string? metadata, 
        DateTime createdAt, DateTime? completedAt) 
        : this(jsonData, formId, formDefinitionId, isComplete, currentPage, metadata)
    {
        Id = id;
        CreatedAt = createdAt;
        CompletedAt = completedAt;
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

    private void SetCompletionStatus(bool newIsCompleteValue)
    {
        if (!IsComplete && newIsCompleteValue)
        {
            IsComplete = true;
            CompletedAt = DateTime.UtcNow;
        }
    }
}
