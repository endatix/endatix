using Endatix.Core.Entities;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Exporting.Exporters.Dynamic;

/// <summary>
/// Exporter for Crunch.io Shoji codebook JSON (<see cref="DynamicExportRow"/>).
/// </summary>
public sealed class ShojiCodebookJsonExporter(ILogger<CodebookJsonExporter> logger) : CodebookJsonExporter(logger)
{
    /// <inheritdoc/>
    public override string Format => "codebook-shoji";

    /// <inheritdoc/>
    protected override string FileNamePrefix => "codebook-shoji";
}
