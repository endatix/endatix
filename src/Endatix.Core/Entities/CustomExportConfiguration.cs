namespace Endatix.Core.Entities;

/// <summary>
/// Represents a custom export configuration for a tenant.
/// </summary>
public class CustomExportConfiguration
{
    /// <summary>
    /// The ID of the custom export configuration.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The name of the custom export configuration.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The name of the SQL function to call for the custom export.
    /// </summary>
    public required string SqlFunctionName { get; set; }

    /// <summary>
    /// The format identifier for the exporter to use (e.g., "csv", "json", "shoji").
    /// </summary>
    public string? Format { get; set; }
}
