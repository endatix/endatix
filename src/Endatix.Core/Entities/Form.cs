using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

  public partial class Form : BaseEntity, IAggregateRoot
  {
      public string Name { get; set; }
      public string? Description { get; set; }
      public bool IsEnabled { get; set; }
      public FormDefinition ActiveFormDefinition => FormDefinitions?.FirstOrDefault(fd => fd.IsActive);
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

        _formDefinitions.Add(new FormDefinition(isDraft: false, formDefinitionJson, isActive: true));
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

    internal void SetActiveFormDefinition(FormDefinition formDefinition)
    {
        throw new System.NotImplementedException(); // TBD and implemented when versioning is fully supported, old implementation is in Git
    }
}