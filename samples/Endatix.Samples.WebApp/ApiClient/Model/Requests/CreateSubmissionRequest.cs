namespace Endatix.Samples.WebApp.ApiClient.Model.Requests;

public class CreateSubmissionRequest
{
    /// <summary>
    /// The ID of the form for which the submission is made.
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// Boolean flag to indicate if a submission is complete. Optional
    /// </summary>
    public bool? IsComplete { get; set; }

    /// <summary>
    /// Current page if the form has multiple pages. Optional
    /// </summary>
    public int? CurrentPage { get; set; }

    /// <summary>
    /// Stringified form submission data
    /// </summary>
    public string JsonData { get; set; }

    /// <summary>
    /// Stringified metadata related to the form submission
    /// </summary>
    public string? Metadata { get; set; }
}
