namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// How a personalization snapshot came onto a submission: the <c>source</c> it is written with and read back as.
/// </summary>
public static class PersonalizationSources
{
    /// <summary>The first open of a personalised link.</summary>
    public const string Link = "link";

    /// <summary>A submission created on behalf of a person by a trusted caller.</summary>
    public const string OnBehalf = "on_behalf";

    /// <summary>A test submission opened as a person by a survey builder.</summary>
    public const string Test = "test";
}
