using System.ComponentModel.DataAnnotations;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Request payload to be used with <see cref="Endatix.Api.Submissions.Create"/> endpoint
/// </summary>
public class CreateSubmissionRequest : BaseSubmissionRequest
{
    /// <summary>
    /// Optional identifier of the user who submitted the form.
    /// When not provided, defaults to the current authenticated user's ID.
    /// Cannot be an empty string.
    /// </summary>
    public string? SubmittedBy { get; set; }
}
