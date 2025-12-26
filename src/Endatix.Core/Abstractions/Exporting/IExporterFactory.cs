namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Factory for creating the appropriate exporter for a given format.
/// </summary>
public interface IExporterFactory
{
    /// <summary>
    /// Gets an exporter for the specified format and record type.
    /// </summary>
    /// <typeparam name="T">The type of records to export.</typeparam>
    /// <param name="format">The export format (e.g., "csv", "json", "excel").</param>
    /// <returns>An exporter for the specified format.</returns>
    IExporter<T> GetExporter<T>(string format) where T : class;

    /// <summary>
    /// Gets all exporters for the specified record type.
    /// </summary>
    /// <typeparam name="T">The type of records to export.</typeparam>
    /// <returns>All exporters for the specified record type.</returns>
    IReadOnlyList<Type> GetSupportedExporters<T>() where T : class;

    /// <summary>
    /// Gets all supported formats for the specified record type.
    /// </summary>
    /// <typeparam name="T">The type of records to export.</typeparam>
    /// <returns>All supported formats for the specified record type.</returns>
    IReadOnlyList<string> GetSupportedFormats<T>() where T : class;
}