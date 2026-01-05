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

            var formId = options?.Metadata != null && options.Metadata.TryGetValue("FormId", out var value) ? value.ToString() : "unknown";
            return Result<FileExport>.Success(new FileExport(ContentType, $"submissions-{formId}.json"));
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
        var formId = options?.Metadata != null && options.Metadata.TryGetValue("FormId", out var value) ? value.ToString() : "unknown";
        return Task.FromResult(Result<FileExport>.Success(new FileExport(ContentType, $"codebook-{formId}.json")));
    }
}
