namespace Endatix.Infrastructure.Identity;

/// <summary>
/// Configuration options for controlling data-related settings in the Endatix application.
/// </summary>
public class DataOptions
{
    /// <summary>
    /// The configuration section name where these options are stored.
    /// </summary>
    public static readonly string SECTION_NAME = "Endatix:Data";

    /// <summary>
    /// Determines whether to apply database migrations.
    /// Set to true to enable database migrations.
    /// Omit or set to false will prevent migrations from running unless explicitly set to true
    /// </summary>
    public bool ApplyMigrations { get; set; } = false;
}
