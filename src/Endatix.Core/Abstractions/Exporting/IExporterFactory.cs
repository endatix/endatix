namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Factory for creating the appropriate exporter for a given format.
/// </summary>
public interface IExporterFactory
{
    /// <summary>
    /// Gets an exporter for the specified format and record type.
    /// </summary>
    /// <typeparam name="T">The type of records to export. Must implement <see cref="IExportItem"/>.</typeparam>
    /// <param name="format">The export format (e.g., "csv", "json", "excel").</param>
    /// <returns>An exporter for the specified format.</returns>
    IExporter<T> GetExporter<T>(string format) where T : class, IExportItem;

    /// <summary>
    /// Gets an exporter for the specified format and item type.
    /// This method provides type-safe resolution when the type is known at runtime.
    /// </summary>
    /// <param name="format">The export format (e.g., "csv", "json", "excel").</param>
    /// <param name="itemType">The type of records to export.</param>
    /// <returns>An exporter for the specified format and type.</returns>
    IExporter GetExporter(string format, Type itemType);

    /// <summary>
    /// Gets all exporters for the specified record type.
    /// </summary>
    /// <typeparam name="T">The type of records to export. Must implement <see cref="IExportItem"/>.</typeparam>
    /// <returns>All exporters for the specified record type.</returns>
    IReadOnlyList<Type> GetSupportedExporters<T>() where T : class, IExportItem;

    /// <summary>
    /// Gets all supported formats for the specified record type.
    /// </summary>
    /// <typeparam name="T">The type of records to export. Must implement <see cref="IExportItem"/>.</typeparam>
    /// <returns>All supported formats for the specified record type.</returns>
    IReadOnlyList<string> GetSupportedFormats<T>() where T : class, IExportItem;
}