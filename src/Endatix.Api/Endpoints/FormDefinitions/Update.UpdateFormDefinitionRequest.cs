namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Request model for updating a form definition.
/// </summary>
public class UpdateFormDefinitionRequest
{
    /// <summary>
    /// The ID of the form.
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// The ID of the form definition.
    /// </summary>
    public long DefinitionId { get; set; }
    
    /// <summary>
    /// Indicates if the form definition is a draft.
    /// </summary>
    public bool? IsDraft { get; set; }

    /// <summary>
    /// The JSON data of the form definition.
    /// </summary>
    public string? JsonData { get; set; }

    /// <summary>
    /// Indicates if the form definition is active.
    /// </summary>
    public bool? IsActive { get; set; }
}
