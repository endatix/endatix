namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Request model for deleting a form.
/// </summary>
public class DeleteFormRequest
{
    /// <summary>
    /// The ID of the form to delete.
    /// </summary>
    public long FormId { get; init; }
}
