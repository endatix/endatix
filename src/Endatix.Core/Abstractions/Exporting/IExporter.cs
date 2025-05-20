using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Abstraction for exporting data of a specific type to a stream.
/// </summary>
/// <typeparam name="T">The type of records to export.</typeparam>
public interface IExporter<T>
{
    /// <summary>
    /// Streams the export of the given records to the provided output stream.
    /// </summary>
    /// <param name="records">The records to export.</param>
    /// <param name="options">Optional export options for customization (columns, formatters, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="outputStream">The output stream to write the export to.</param>
    /// <returns>The exported file result (file name and content type).</returns>
    Task<ExportFileResult> StreamExportAsync(
        IAsyncEnumerable<T> records,
        ExportOptions options,
        CancellationToken cancellationToken,
        Stream outputStream);
}

/// <summary>
/// Represents the result of an export operation.
/// </summary>
public sealed record ExportFileResult(
    string ContentType,
    string FileName
);

/// <summary>
/// Options for customizing export operations.
/// </summary>
public class ExportOptions
{
    /// <summary>
    /// Optional columns to include in the export. If null, all columns are included.
    /// </summary>
    public IEnumerable<string>? Columns { get; set; }
    
    /// <summary>
    /// Optional dictionary of column transformers to customize how values are formatted.
    /// Keys are column names, values are functions that transform the original value to a string.
    /// </summary>
    public IDictionary<string, System.Func<object?, string>>? Transformers { get; set; }
} 