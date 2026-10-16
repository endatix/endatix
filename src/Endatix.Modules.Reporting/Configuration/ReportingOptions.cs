namespace Endatix.Modules.Reporting.Configuration;

/// <summary>
/// Configuration for the Reporting module.
/// </summary>
public sealed class ReportingOptions
{
    public const string SECTION_NAME = "Customizations:Reporting";

    /// <summary>
    /// When true, applies the active provider's EF migrations for <see cref="Persistence.ReportingDbContextBase"/>
    /// at application startup.
    /// </summary>
    public bool ApplyMigrationsAtStartup { get; set; } = true;

    /// <summary>
    /// The submitter profile fields copied into prepared submissions, each as a <c>submitter_&lt;field&gt;</c> column.
    /// A field is a key of the submission's profile snapshot. Empty (the default) adds no column.
    /// </summary>
    /// <remarks>The snapshot holds personal data, so only the fields named here reach reporting.</remarks>
    public List<string> SubmitterProfileFields { get; set; } = [];
}
