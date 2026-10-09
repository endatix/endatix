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
}
