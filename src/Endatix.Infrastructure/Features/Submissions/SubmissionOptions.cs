using Endatix.Framework.Configuration;

namespace Endatix.Infrastructure.Features.Submissions;

/// <summary>
/// Configuration options for submissions.
/// </summary>
public class SubmissionOptions : EndatixOptionsBase
{
    /// <summary>
    /// Gets the section path for these options.
    /// </summary>
    public override string SectionPath => "Submissions";

    /// <summary>
    /// Gets or sets the token expiry in hours.
    /// </summary>
    public int TokenExpiryInHours { get; set; } = 24;
}
