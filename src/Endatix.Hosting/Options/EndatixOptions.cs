namespace Endatix.Hosting.Options;

/// <summary>
/// Global configuration options for Endatix.
/// </summary>
public class EndatixOptions
{
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
    
    /// <summary>
    /// Gets or sets whether Azure integration is enabled.
    /// </summary>
    public bool IsAzure { get; set; } = false;
} 