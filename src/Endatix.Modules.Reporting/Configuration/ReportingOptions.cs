using System.ComponentModel.DataAnnotations;

namespace Endatix.Modules.Reporting.Configuration;

/// <summary>
/// Configuration for the Reporting module.
/// </summary>
public sealed class ReportingOptions
{
    public const string SECTION_NAME = "Customizations:Reporting";
    internal const int DEFAULT_MAX_QUESTIONS = 1_500;

    /// <summary>The largest question count reporting will compile when there is no tenant override.</summary>
    [Range(1, int.MaxValue)]
    public int MaxQuestions { get; set; } = DEFAULT_MAX_QUESTIONS;

    /// <summary>Operator-configured question limits keyed by tenant ID.</summary>
    public Dictionary<long, int> MaxQuestionsByTenant { get; set; } = [];

    /// <summary>Returns the inclusive limit for the tenant that owns the definition.</summary>
    public int GetMaxQuestions(long tenantId) => MaxQuestionsByTenant.GetValueOrDefault(tenantId, MaxQuestions);

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
