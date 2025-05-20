using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Exporting;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Exporting;

/// <summary>
/// Implementation of IExporterFactory that creates exporters based on format.
/// </summary>
internal sealed class ExporterFactory : IExporterFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ExporterFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets the appropriate exporter for the given format and type.
    /// </summary>
    /// <typeparam name="T">The type of records to export.</typeparam>
    /// <param name="format">The export format.</param>
    /// <returns>An exporter for the specified format.</returns>
    /// <exception cref="ArgumentException">Thrown when the format is not supported.</exception>
    public IExporter<T> GetExporter<T>(string format) where T : class
    {
        Guard.Against.Null(format, nameof(format));

        format = format.ToLowerInvariant();

        // Currently only supporting CSV format. 
        // This can be expanded in the future to support more formats with a switch statement
        if (format == "csv")
        {
            var exporter = _serviceProvider.GetService<IExporter<T>>() ?? throw new InvalidOperationException($"No exporter registered for type {typeof(T).Name} and format {format}");

            return exporter;
        }

        throw new ArgumentException($"Export format '{format}' is not supported", nameof(format));
    }
}