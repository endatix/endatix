namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Request model for getting a form definition by ID.
/// </summary>
public class GetActiveFormDefinitionRequest
{
    /// <summary>
    /// The ID of the form.
    /// </summary>
    public long FormId { get; set; }
}
