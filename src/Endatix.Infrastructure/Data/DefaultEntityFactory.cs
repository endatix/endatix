using Endatix.Core.Abstractions;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Data;

public class DefaultEntityFactory : IEntityFactory
{
    public Form CreateForm(string name, string? description = null, bool isEnabled = false)
    {
        return new Form(name, description, isEnabled);
    }

    public FormDefinition CreateFormDefinition(bool isDraft = false, string? jsonData = null)
    {
        return new FormDefinition(isDraft, jsonData);
    }

    public Submission CreateSubmission(string jsonData, long formId, long formDefinitionId, bool isComplete = true, int currentPage = 1, string? metadata = null)
    {
        return new Submission(jsonData, formId, formDefinitionId, isComplete, currentPage, metadata);
    }
}
