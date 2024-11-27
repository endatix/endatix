using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

public partial class Form : BaseEntity, IAggregateRoot
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public FormDefinition? ActiveDefinition { get; private set; }
    public long? ActiveDefinitionId { get; private set; }

    private readonly List<Submission> _submissions = [];
    private readonly List<FormDefinition> _formDefinitions = [];
    public IReadOnlyCollection<FormDefinition> FormDefinitions => _formDefinitions.AsReadOnly();

    public Form()
    {

    }

    public Form(string name, string? description = null, bool isEnabled = false, string? formDefinitionJson = null)
    {
        Guard.Against.NullOrEmpty(name, null, "Form name cannot be null.");
        Name = name;
        Description = description;
        IsEnabled = isEnabled;

        AddFormDefinition(formDefinitionJson, isDraft: false);
    }

    public void AddSubmission(string jsonData, long formDefintionId, bool isComplete = true, int currentPage = 1, string metadata = null)
    {
        Guard.Against.NegativeOrZero(currentPage, nameof(currentPage));
        Guard.Against.NullOrEmpty(jsonData, nameof(jsonData));

        _submissions.Add(new Submission(jsonData, formDefintionId, isComplete, currentPage, metadata));
    }

    public void UpdateSubmission(long submissionId, long formDefintionId, string jsonData, bool isComplete = true, int currentPage = 1)
    {
        Guard.Against.NegativeOrZero(currentPage, nameof(currentPage));
        Guard.Against.Null(submissionId, nameof(submissionId));

        var submission = _submissions.FirstOrDefault(r => r.Id.Equals(submissionId));

        Guard.Against.Null(submission, nameof(submission));

        submission.Update(jsonData, formDefintionId, isComplete, currentPage);
    }

    public void SetActiveFormDefinition(FormDefinition formDefinition)
    {
        Guard.Against.Null(formDefinition, nameof(formDefinition));

        if (!_formDefinitions.Contains(formDefinition))
        {
            throw new InvalidOperationException("Cannot set a FormDefinition as active that doesn't belong to this form.");
        }

        if (ActiveDefinition != null)
        {
            ActiveDefinition.IsActive = false;
        }

        formDefinition.IsActive = true;
        ActiveDefinition = formDefinition;
        ActiveDefinitionId = formDefinition.Id;
    }

    public void AddFormDefinition(string jsonData, bool isDraft = false)
    {
        var formDefinition = new FormDefinition(isDraft, jsonData);
        _formDefinitions.Add(formDefinition);

        if (_formDefinitions.Count == 1)
        {
            SetActiveFormDefinition(formDefinition);
        }
    }
}