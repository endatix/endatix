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
    /// The format identifier for the exporter to use (e.g., "csv", "json", "codebook").
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// The fully qualified type name of the export row type (e.g., "Endatix.Core.Entities.SubmissionExportRow").
    /// Used to uniquely identify the exporter when multiple exporters share the same format.
    /// </summary>
    public string? ItemTypeName { get; set; }
}
