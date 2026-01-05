using System.IO.Pipelines;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Abstraction for exporting data to a stream. Used as metadata for the <see cref="IExporterFactory"/> to make this DI friendly.
/// </summary>
public interface IExporter
{
    /// <summary>
    /// Gets the format identifier for this exporter (e.g., "csv", "json", "excel").
    /// </summary>
    string Format { get; }

    /// <summary>
    /// Gets the type of items that this exporter can export.
    /// </summary>
    Type ItemType { get; }

    /// <summary>
    /// Gets the HTTP headers for the export without processing data.
    /// This is a non-generic method that can be called without knowing the specific type.
    /// </summary>
    /// <param name="options">Export options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Export headers or error information.</returns>
    Task<Result<FileExport>> GetHeadersAsync(
        ExportOptions? options,
        CancellationToken cancellationToken);

    /// <summary>
    /// Streams the export directly to the provided PipeWriter using a data provider function.
    /// This non-generic method allows calling exporters without reflection when the type is only known at runtime.
    /// </summary>
    /// <param name="getDataAsync">A function that returns the data to export. The exporter will call this with its known type parameter.</param>
    /// <param name="options">Export options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="writer">The PipeWriter to write to.</param>
    /// <returns>A result containing export metadata or error information.</returns>
    Task<Result<FileExport>> StreamExportAsync(
        Func<Type, IAsyncEnumerable<IExportItem>> getDataAsync,
        ExportOptions? options,
        CancellationToken cancellationToken,
        PipeWriter writer);
}

/// <summary>
/// Abstraction for exporting data of a specific type to a stream.
/// </summary>
/// <typeparam name="T">The type of records to export. Must implement <see cref="IExportItem"/>.</typeparam>
public interface IExporter<T> : IExporter where T : class, IExportItem
{

    /// <inheritdoc/>
    /// <summary>
    /// Gets the type of items that this exporter can export from the generic type parameter.
    /// </summary>
    Type IExporter.ItemType => typeof(T);

    /// <summary>
    /// Streams the export directly to the provided PipeWriter.
    /// </summary>
    /// <param name="records">The records to export.</param>
    /// <param name="options">Export options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="writer">The PipeWriter to write to.</param>
    /// <returns>A result containing export metadata or error information.</returns>
    Task<Result<FileExport>> StreamExportAsync(
        IAsyncEnumerable<T> records,
        ExportOptions? options,
        CancellationToken cancellationToken,
        PipeWriter writer);

    /// <inheritdoc/>
    async Task<Result<FileExport>> IExporter.StreamExportAsync(
        Func<Type, IAsyncEnumerable<IExportItem>> getDataAsync,
        ExportOptions? options,
        CancellationToken cancellationToken,
        PipeWriter writer)
    {
        var items = getDataAsync(typeof(T));
        var typedData = CastAsyncEnumerable(items);
        return await StreamExportAsync(typedData, options, cancellationToken, writer);
    }

    private async IAsyncEnumerable<T> CastAsyncEnumerable(IAsyncEnumerable<IExportItem> items)
    {
        await foreach (var item in items)
        {
            if (item is T typedItem)
            {
                yield return typedItem;
            }
        }
    }
}