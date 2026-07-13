using Endatix.Core.Abstractions.Exporting;

namespace Endatix.Infrastructure.Exporting;

/// <summary>
/// Optional internal priority for built-in SQL data sources.
/// Integration slices use the default priority via <see cref="ExportDataSourcePriority.Integration"/>.
/// </summary>
internal interface IPrioritizedExportDataSource
{
    int Priority { get; }
}

internal static class ExportDataSourcePriority
{
    internal const int SqlCustom = 0;
    internal const int Integration = 100;
    internal const int SqlFallback = 1000;
}

/// <summary>
/// Picks the first matching export data source by ascending priority.
/// </summary>
internal sealed class ExportDataSourceResolver(IEnumerable<IExportDataSource> sources) : IExportDataSourceResolver
{
    public IExportDataSource Resolve(ExportDataSourceRequest request)
    {
        var source = sources
            .Where(candidate => candidate.Matches(request))
            .OrderBy(GetPriority)
            .FirstOrDefault();

        if (source is null)
        {
            throw new InvalidOperationException(
                $"No export data source registered for format '{request.Format}' and item type '{request.ItemType.Name}'.");
        }

        return source;
    }

    private static int GetPriority(IExportDataSource source) =>
        source is IPrioritizedExportDataSource prioritized
            ? prioritized.Priority
            : ExportDataSourcePriority.Integration;
}
