namespace Endatix.Infrastructure.Features.Submissions;

/// <summary>
/// Configuration options for submissions
/// </summary>
public class SubmissionOptions
{
    /// <summary>
    /// The configuration section name where these options are stored.
    /// </summary>
    public static readonly string SECTION_NAME = "Endatix:Submissions";

    /// <summary>
    /// The expiration time of the submission token in hours.
    /// </summary>
    public int TokenExpiryInHours { get; set; } = 24;
}
