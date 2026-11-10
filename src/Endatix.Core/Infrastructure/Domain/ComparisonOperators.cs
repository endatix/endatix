namespace Endatix.Core.Infrastructure.Domain;

/// <summary>
/// Null-aware ordering shared by value types that implement the relational operators
/// (<c>null</c> sorts before any non-null value).
/// </summary>
internal static class ComparisonOperators
{
    public static bool LessThan<T>(T? left, T? right) where T : class, IComparable<T> =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool LessThanOrEqual<T>(T? left, T? right) where T : class, IComparable<T> =>
        left is null || left.CompareTo(right) <= 0;
}
