using Endatix.Core.Entities;
using Endatix.Core.Entities.Identity;

namespace Endatix.Core.Abstractions;

/// <summary>
/// Factory interface for creating domain entities. Implementations can provide extended versions of entities.
/// </summary>
public interface IEntityFactory
{
    /// <summary>
    /// Creates a new Form entity.
    /// </summary>
    Form CreateForm(string name, string? description = null, bool isEnabled = false);
    
    /// <summary>
    /// Creates a new FormDefinition entity.
    /// </summary>
    FormDefinition CreateFormDefinition(bool isDraft = false, string? jsonData = null);
    
    /// <summary>
    /// Creates a new Submission entity.
    /// </summary>
    Submission CreateSubmission(string jsonData, long formId, long formDefinitionId, bool isComplete = true, int currentPage = 1, string? metadata = null);
}
