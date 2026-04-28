using Endatix.Core.Infrastructure.Result;

namespace Endatix.Api.Infrastructure;

/// <summary>
/// Extension methods for <see cref="Paged{T}"/>.
/// </summary>
public static class PagedExtensions
{
    /// <summary>
    /// Maps a <see cref="Paged{TSource}"/> to a <see cref="Paged{TDestination}"/> while preserving paging metadata.
    /// </summary>
    /// <typeparam name="TSource">The source item type.</typeparam>
    /// <typeparam name="TDestination">The destination item type.</typeparam>
    /// <param name="source">The source paged data.</param>
    /// <param name="mapper">The mapper function to transform each item.</param>
    /// <returns>A new <see cref="Paged{TDestination}"/> with the mapped items and original paging metadata.</returns>
    public static Paged<IReadOnlyCollection<TDestination>> MapToPaged<TSource, TDestination>(
        this Paged<IReadOnlyCollection<TSource>> source,
        Func<TSource, TDestination> mapper) => new Paged<IReadOnlyCollection<TDestination>>(
            page: source.Page,
            pageSize: source.PageSize,
            totalRecords: source.TotalRecords,
            totalPages: source.TotalPages,
            items: [.. source.Items.Select(mapper)]);

    /// <summary>
    /// Maps a <see cref="Paged{IEnumerable{TSource}}"/> to a <see cref="Paged{IEnumerable{TDestination}}"/> while preserving paging metadata.
    /// </summary>
    /// <typeparam name="TSource">The source item type.</typeparam>
    /// <typeparam name="TDestination">The destination item type.</param>
    /// <param name="source">The source paged data.</param>
    /// <param name="mapper">The mapper function to transform each item.</param>
    /// <returns>A new <see cref="Paged{IEnumerable{TDestination}"/> with the mapped items and original paging metadata.</returns>
    public static Paged<IEnumerable<TDestination>> MapToPaged<TSource, TDestination>(
        this Paged<IEnumerable<TSource>> source,
        Func<TSource, TDestination> mapper) => new Paged<IEnumerable<TDestination>>(
            page: source.Page,
            pageSize: source.PageSize,
            totalRecords: source.TotalRecords,
            totalPages: source.TotalPages,
            items: source.Items.Select(mapper));
}