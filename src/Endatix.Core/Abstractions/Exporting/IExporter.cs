using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Infrastructure.Result;

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
    /// <returns>A result containing export metadata or error information.</returns>
    Task<Result<FileExport>> StreamExportAsync(
        IAsyncEnumerable<T> records,
        ExportOptions? options,
        CancellationToken cancellationToken,
        Stream outputStream);
        
    /// <summary>
    /// Gets the HTTP headers for the export without processing data.
    /// </summary>
    /// <param name="options">Export options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Export headers or error information.</returns>
    Task<Result<FileExport>> GetHeadersAsync(
        ExportOptions? options,
        CancellationToken cancellationToken);
} 