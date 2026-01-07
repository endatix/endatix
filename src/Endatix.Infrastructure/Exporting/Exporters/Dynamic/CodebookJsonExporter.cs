using System.IO.Pipelines;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Exporting.Exporters.Dynamic;

/// <summary>
/// Exporter for Codebook Shoji JSON format, which outputs raw JSON data from the database.
/// </summary>
public sealed class CodebookJsonExporter(ILogger<CodebookJsonExporter> logger) : IExporter<DynamicExportRow>
{
    /// <inheritdoc/>
    public string Format => "codebook";

    /// <inheritdoc/>
    public string ContentType => "application/json";

    /// <inheritdoc/>
    public Type ItemType => typeof(DynamicExportRow);

    /// <inheritdoc/>
    public async Task<Result<FileExport>> StreamExportAsync(
        IAsyncEnumerable<DynamicExportRow> records,
        ExportOptions? options,
        CancellationToken cancellationToken,
        PipeWriter writer)
    {
        try
        {
            var fileHeadersResult = await GetHeadersAsync(options, cancellationToken);
            if (!fileHeadersResult.IsSuccess)
            {
                return fileHeadersResult;
            }
            
            using var stream = writer.AsStream();
            await foreach (var row in records.WithCancellation(cancellationToken))
            {
                if (string.IsNullOrEmpty(row.Data))
                {
                    continue;
                }

                var bytes = System.Text.Encoding.UTF8.GetBytes(row.Data);
                await stream.WriteAsync(bytes, cancellationToken);
            }
            await writer.FlushAsync(cancellationToken);

            return Result.Success(fileHeadersResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error exporting Codebook JSON");
            return Result<FileExport>.Error($"Failed to export Codebook JSON: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<Result<FileExport>> GetHeadersAsync(ExportOptions? options, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = GetFileName(options);
            return Task.FromResult(Result<FileExport>.Success(new FileExport(ContentType, fileName)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Codebook JSON export headers");
            return Task.FromResult(Result<FileExport>.Error($"Failed to get export headers: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gets the file name for the export based on FormId in metadata.
    /// </summary>
    /// <param name="options">Export options containing metadata.</param>
    /// <returns>File name in format "codebook-{formId}.json" or "codebook-unknown.json" if FormId is missing.</returns>
    /// <remarks>
    /// FormId is currently optional. To make it required and return an error early, uncomment the validation below:
    /// <code>
    /// if (options?.Metadata == null || !options.Metadata.TryGetValue("FormId", out var value))
    /// {
    ///     throw new InvalidOperationException("FormId is required in export options metadata for Codebook exports");
    /// }
    /// </code>
    /// </remarks>
    private static string GetFileName(ExportOptions? options)
    {
        if (options?.Metadata != null && options.Metadata.TryGetValue("FormId", out var value))
        {
            return $"codebook-{value}.json";
        }

        // FormId is optional - use "unknown" as fallback
        // To make FormId required, replace this return with validation above
        return "codebook-unknown.json";
    }
}
