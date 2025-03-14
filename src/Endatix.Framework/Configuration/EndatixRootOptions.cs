namespace Endatix.Framework.Configuration;

/// <summary>
/// Root configuration options for Endatix.
/// </summary>
public class EndatixRootOptions : EndatixOptionsBase
{
    /// <summary>
    /// Gets the section path for these options.
    /// An empty string indicates this is the root section.
    /// </summary>
    public override string SectionPath => string.Empty;
    
    /// <summary>
    /// Gets or sets the application name.
    /// </summary>
    public string ApplicationName { get; set; } = "Endatix API";

    /// <summary>
    /// Gets or sets whether telemetry is enabled.
    /// </summary>
    public bool EnableTelemetry { get; set; } = false;

    /// <summary>
    /// Gets or sets the environment name.
    /// </summary>
    public string Environment { get; set; } = "Development";
} 