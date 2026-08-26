namespace Endatix.Core.Infrastructure.Paging;

/// <summary>
/// Inclusive UTC lower bound and exclusive UTC upper bound for a single timestamp column.
/// Built at the API boundary from calendar day strings (<c>YYYY-MM-DD</c>).
/// </summary>
public readonly record struct UtcDateTimeRange(
    DateTime? InclusiveFrom,
    DateTime? ExclusiveTo)
{
    /// <summary>
    /// True when either bound is present.
    /// </summary>
    public bool HasBounds => InclusiveFrom.HasValue || ExclusiveTo.HasValue;
}
