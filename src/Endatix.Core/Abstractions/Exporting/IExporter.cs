using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Abstraction for exporting data of a specific type to a stream.
/// </summary>
/// <typeparam name="T">The type of records to export.</typeparam>
public interface IExporter<T> where T : class
{
    /// <summary>
    /// Streams the export directly to the provided output stream.
    /// </summary>
    /// <param name="records">The records to export.</param>
    /// <param name="options">Export options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="outputStream">The stream to write to.</param>
    /// <returns>A result containing export metadata.</returns>
    Task<ExportFileResult> StreamExportAsync(
        IAsyncEnumerable<T> records,
        ExportOptions? options,
        CancellationToken cancellationToken,
        Stream outputStream);
        
    /// <summary>
    /// Gets the HTTP headers for the export without processing data.
    /// </summary>
    /// <param name="options">Export options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Export headers.</returns>
    Task<ExportHeaders> GetHeadersAsync(
        ExportOptions? options,
        CancellationToken cancellationToken);
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
    
    /// <summary>
    /// Optional metadata for export operations. Can be used to pass additional context.
    /// </summary>
    public IDictionary<string, object>? Metadata { get; set; } = new Dictionary<string, object>();
} 