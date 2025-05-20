using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Abstraction for exporting data in various formats (CSV, JSON, Excel, GoogleDrive, etc.), supporting streaming to an output stream.
/// </summary>
public interface IExporter
{
    /// <summary>
    /// Streams the export of the given records into the specified format to the provided output stream.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="records">The records to export.</param>
    /// <param name="columns">The columns to include in the export.</param>
    /// <param name="format">The export format (e.g., "csv", "json", "excel", "googledrive").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="outputStream">The output stream to write the export to.</param>
    /// <returns>The exported file result (file name and content type).</returns>
    Task<ExportFileResult> StreamExportAsync<T>(
        IAsyncEnumerable<T> records,
        IEnumerable<string> columns,
        string format,
        CancellationToken cancellationToken,
        Stream outputStream);
}

/// <summary>
/// Represents the result of an export operation.
/// </summary>
public sealed record ExportFileResult(
    byte[] FileContent,
    string ContentType,
    string FileName
); 