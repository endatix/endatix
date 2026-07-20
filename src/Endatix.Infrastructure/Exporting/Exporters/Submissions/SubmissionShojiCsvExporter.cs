using Endatix.Core.Abstractions.Exporting;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Exporting.Exporters.Submissions;

/// <summary>
/// Crunch/Shoji CSV exporter — same streaming as <see cref="SubmissionCsvExporter"/> with wire key <c>csv-shoji</c>.
/// </summary>
public sealed class SubmissionShojiCsvExporter(
    ILogger<SubmissionShojiCsvExporter> logger,
    IEnumerable<IValueTransformer> globalTransformers) : SubmissionCsvExporter(logger, globalTransformers)
{
    public override string Format => "csv-shoji";
}
