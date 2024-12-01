using Ardalis.GuardClauses;
using Endatix.Core.Configuration;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

public partial class FormDefinition : BaseEntity, IAggregateRoot
{
    private readonly List<Submission> _submissions = [];

    public FormDefinition(Form form, bool isDraft = false, string? jsonData = null, bool isActive = true)
    {
        Guard.Against.Null(form, nameof(form));

        Form = form;
        FormId = form.Id;
        jsonData ??= EndatixConfig.Configuration.DefaultFormDefinitionJson;
        IsDraft = isDraft;
        JsonData = jsonData;
        IsActive = isActive;
    }

    public bool IsDraft { get; private set; }
    public int Version { get; private set; }
    public string JsonData { get; private set; }
    public long FormId { get; private set; }
    public Form Form { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<Submission> Submissions => _submissions.AsReadOnly();

    /// <summary>
    /// Update the form definition with the provided data.
    /// </summary>
    /// <param name="jsonData">The new JSON data for the form definition.</param>
    /// <param name="isDraft">The new draft status for the form definition.</param>
    /// <param name="isActive">The new active status for the form definition.</param>
    public void Update(string? jsonData, bool? isDraft, bool? isActive)
    {
        JsonData = jsonData ?? JsonData;
        IsDraft = isDraft ?? IsDraft;
        IsActive = isActive ?? IsActive;
    }
}
