using Endatix.Core.Abstractions.Exporting;

namespace Endatix.Infrastructure.Exporting;

/// <summary>
/// Implementation of IExporterFactory that creates exporters based on format.
/// </summary>
internal sealed class ExporterFactory : IExporterFactory
{
    private readonly IEnumerable<IExporter> _exporters;

    public ExporterFactory(IEnumerable<IExporter> exporters)
    {
        _exporters = exporters;
    }

    public IExporter<T> GetExporter<T>(string format) where T : class
    {
        var exporter = _exporters
                .Where(e => e.ItemType == typeof(T) &&
                            e.Format.Equals(format, StringComparison.OrdinalIgnoreCase))
                .Cast<IExporter<T>>()
                .FirstOrDefault();

        if (exporter is null)
        {
            throw new InvalidOperationException($"No exporter registered for type {typeof(T).Name} and format {format}");
        }

        return exporter;
    }

    /// <inheritdoc/>
    public IExporter GetExporter(string format)
    {
        var exporter = _exporters
                .FirstOrDefault(e => e.Format.Equals(format, StringComparison.OrdinalIgnoreCase));

        if (exporter is null)
        {
            throw new InvalidOperationException($"No exporter registered for format {format}");
        }

        return exporter;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Type> GetSupportedExporters<T>() where T : class => _exporters
        .Where(e => e.ItemType == typeof(T))
        .Select(e => e.GetType())
        .Distinct()
        .ToList()
        .AsReadOnly();

    /// <inheritdoc/>
    public IReadOnlyList<string> GetSupportedFormats<T>() where T : class => _exporters
        .Where(e => e.ItemType == typeof(T))
        .Select(e => e.Format)
        .Distinct()
        .ToList()
        .AsReadOnly();
}