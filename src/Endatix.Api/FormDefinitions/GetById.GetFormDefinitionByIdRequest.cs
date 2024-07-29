namespace Endatix.Api.FormDefinitions;

/// <summary>
/// Request model for getting a form definition by ID.
/// </summary>
public class GetFormDefinitionByIdRequest
{
    /// <summary>
    /// The ID of the form.
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// The ID of the form definition.
    /// </summary>
    public long DefinitionId { get; set; }
}
