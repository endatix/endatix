using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

  public partial class Submission : BaseEntity, IAggregateRoot
  {
      public bool IsComplete { get; private set; }
      public string JsonData { get; private set; }
      public long FormDefinitionId { get; set; }
      public FormDefinition FormDefinition { get; set; }
      public int? CurrentPage { get; private set; }
      public string? Metadata { get; private set; }
      public DateTime? CompletedAt { get; private set; }

    public Submission()
    {

    }

    public Submission(string jsonData, long? formDefinitionId = null, bool isComplete = true, int currentPage = 1, string metadata = null)
    {
        Guard.Against.NullOrEmpty(jsonData, nameof(jsonData));

        if (formDefinitionId != null)
        {
            Guard.Against.NegativeOrZero(formDefinitionId.Value, nameof(formDefinitionId));
            FormDefinitionId = formDefinitionId.Value;
        }

        JsonData = jsonData;
        CurrentPage = currentPage;
        Metadata = metadata;

        SetCompletionStatus(isComplete);
    }

    public void Update(string jsonData, long formDefinitionId, bool isComplete = true, int currentPage = 1, string metadata = null)
    {
        Guard.Against.NullOrEmpty(jsonData, nameof(jsonData));
        Guard.Against.NegativeOrZero(formDefinitionId, nameof(formDefinitionId));

        FormDefinitionId = formDefinitionId;
        JsonData = jsonData;
        CurrentPage = currentPage;
        Metadata = metadata;

        SetCompletionStatus(isComplete);
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
