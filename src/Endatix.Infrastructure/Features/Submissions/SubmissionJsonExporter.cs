using System.IO.Pipelines;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.Submissions;

/// <summary>
/// JSON exporter for submission data, optimized for streaming and low memory usage.
/// </summary>
public sealed class SubmissionJsonExporter : IExporter<SubmissionExportRow>
{
    private const string JSON_CONTENT_TYPE = "application/json";

    /// <inheritdoc/>
    public string Format => "json";

    /// <inheritdoc/>
    public Type ItemType => typeof(SubmissionExportRow);

    private readonly ILogger<SubmissionJsonExporter> _logger;

    public SubmissionJsonExporter(ILogger<SubmissionJsonExporter> logger)
    {
        _logger = logger;
    }

    // <inheritdoc/>
    public Task<Result<FileExport>> GetHeadersAsync(ExportOptions? options, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = "submissions.json";
            var fileExport = new FileExport(JSON_CONTENT_TYPE, fileName);
            return Task.FromResult(Result<FileExport>.Success(fileExport));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting export headers");
            return Task.FromResult(Result<FileExport>.Error($"Failed to get export headers: {ex.Message}"));
        }
    }

    public Task<Result<FileExport>> StreamExportAsync(IAsyncEnumerable<SubmissionExportRow> records, ExportOptions? options, CancellationToken cancellationToken, PipeWriter writer) => throw new NotImplementedException();
}
