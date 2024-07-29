using Endatix.Core.Entities;

namespace Endatix.Api.FormDefinitions;

/// <summary>
/// Mapper from a form definition entity to a form definition API model.
/// </summary>
public class FormDefinitionMapper
{
    /// <summary>
    /// Maps a form definition entity to a form definition API model.
    /// </summary>
    /// <typeparam name="T">The type of the form definition API model, which inherits FormDefinitionModel.</typeparam>
    /// <param name="formDefinition">The form definition entity.</param>
    /// <returns>The mapped form definition API model.</returns>
    public static T Map<T>(FormDefinition formDefinition) where T : FormDefinitionModel, new() => new T
    {
        Id = formDefinition.Id.ToString(),
        IsDraft = formDefinition.IsDraft,
        JsonData = formDefinition.JsonData,
        FormId = formDefinition.FormId.ToString(),
        IsActive = formDefinition.IsActive,
        CreatedAt = formDefinition.CreatedAt,
        ModifiedAt = formDefinition.ModifiedAt
    };
    
    /// <summary>
    /// Maps a collection of form definition entities to a collection of form definition API models.
    /// </summary>
    /// <typeparam name="T">The type of the form definition API model, which inherits FormDefinitionModel.</typeparam>
    /// <param name="formDefinitions">The collection of form definition entities.</param>
    /// <returns>A collection of mapped form definition API models.</returns>
    public static IEnumerable<T> Map<T>(IEnumerable<FormDefinition> formDefinitions) where T : FormDefinitionModel, new() =>
        formDefinitions.Select(Map<T>).ToList();
}
