namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Request model for retrieving form definition fields.
/// </summary>
public class GetFieldsRequest
{
    /// <summary>
    /// The ID of the form whose definition fields should be retrieved.
    /// </summary>
    public long FormId { get; set; }
}
