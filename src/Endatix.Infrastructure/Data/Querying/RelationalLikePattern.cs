namespace Endatix.Infrastructure.Data.Querying;

/// <summary>
/// Builds escaped LIKE / ILIKE substring patterns shared by provider-specific filters.
/// </summary>
public static class RelationalLikePattern
{
    /// <summary>
    /// Builds a %…% pattern with metacharacters in <paramref name="trimmedQuery"/> escaped for LIKE.
    /// </summary>
    /// <param name="trimmedQuery">The trimmed query to build the pattern for.</param>
    /// <param name="sqlServerLike">When true, escapes '[' for SQL Server LIKE bracket rules.</param>
    public static string BuildContainsPattern(string trimmedQuery, bool sqlServerLike)
    {
        var escaped = EscapeLikeLiteral(trimmedQuery, sqlServerLike);
        return '%' + escaped + '%';
    }

    private static string EscapeLikeLiteral(string value, bool sqlServerLike)
    {
        var s = value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return sqlServerLike ? s.Replace("[", "[[]", StringComparison.Ordinal) : s;
    }
}
