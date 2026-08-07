namespace Endatix.Infrastructure.Data.Querying;

/// <summary>
/// Builds escaped LIKE / ILIKE patterns shared by provider-specific filters.
/// </summary>
public static class RelationalLikePattern
{
    /// <summary>
    /// Builds a %…% pattern with metacharacters in <paramref name="trimmedQuery"/> escaped for LIKE.
    /// </summary>
    public static string BuildContainsPattern(string trimmedQuery, bool sqlServerLike)
        => BuildPattern(trimmedQuery, RelationalTextMatchMode.Contains, sqlServerLike);

    /// <summary>
    /// Builds a LIKE/ILIKE pattern for the given match mode.
    /// </summary>
    public static string BuildPattern(string trimmedQuery, RelationalTextMatchMode matchMode, bool sqlServerLike)
    {
        var escaped = EscapeLikeLiteral(trimmedQuery, sqlServerLike);
        return matchMode switch
        {
            RelationalTextMatchMode.Exact => escaped,
            RelationalTextMatchMode.StartsWith => escaped + '%',
            _ => '%' + escaped + '%'
        };
    }

    private static string EscapeLikeLiteral(string value, bool sqlServerLike)
    {
        var s = value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return sqlServerLike ? s.Replace("[", "[[]", StringComparison.Ordinal) : s;
    }
}
