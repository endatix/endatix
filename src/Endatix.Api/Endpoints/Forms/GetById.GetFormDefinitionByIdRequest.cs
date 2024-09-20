namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Request model for getting a form by ID.
/// </summary>
public class GetFormByIdRequest
{
    /// <summary>
    /// The ID of the form.
    /// </summary>
    public long FormId { get; set; }
}
