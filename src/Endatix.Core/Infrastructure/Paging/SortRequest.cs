namespace Endatix.Core.Infrastructure.Paging;

/// <summary>
/// Normalized sort input for list queries and read models.
/// </summary>
/// <typeparam name="TSortField">The closed set of sortable fields for the list.</typeparam>
public sealed record SortRequest<TSortField>(TSortField Field, SortDirection Direction)
    where TSortField : struct, Enum
{
    /// <summary>
    /// Creates a new <see cref="SortRequest{TSortField}"/> from a sort by and direction.
    /// </summary>
    /// <param name="sortBy">The sort by.</param>
    /// <param name="direction">The direction.</param>
    /// <param name="defaultField">The default field.</param>
    /// <param name="defaultDirection">The default direction.</param>
    /// <returns>A new <see cref="SortRequest{TSortField}"/>.</returns>
    public static SortRequest<TSortField>? FromNullable(
        TSortField? sortBy,
        SortDirection? direction,
        TSortField defaultField,
        SortDirection defaultDirection = SortDirection.Asc)
    {
        if (sortBy is null && direction is null)
        {
            return null;
        }

        return new SortRequest<TSortField>(
            sortBy ?? defaultField,
            direction ?? defaultDirection);
    }

    /// <summary>
    /// Creates a new <see cref="SortRequest{TSortField}"/> from a sort by and direction.
    /// </summary>
    /// <param name="sortBy">The sort by.</param>
    /// <param name="direction">The direction.</param>
    /// <param name="defaultField">The default field.</param>
    /// <param name="defaultDirection">The default direction.</param>
    /// <returns>A new <see cref="SortRequest{TSortField}"/>.</returns>
    public static SortRequest<TSortField> FromNullableOrDefault(
        TSortField? sortBy,
        SortDirection? direction,
        TSortField defaultField,
        SortDirection defaultDirection = SortDirection.Asc) =>
        FromNullable(sortBy, direction, defaultField, defaultDirection)
        ?? new SortRequest<TSortField>(defaultField, defaultDirection);
}
