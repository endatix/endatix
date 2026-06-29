using Endatix.Infrastructure.Data.Querying;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Persistence.PostgreSql.Querying;

/// <summary>
/// Case-insensitive substring match via ILIKE with ESCAPE.
/// </summary>
public sealed class NpgsqlSubstringLikeFilter : IRelationalSubstringLikeFilter
{
    /// <inheritdoc />
    public IQueryable<TEntity> WherePropertyMatchesLikeSubstring<TEntity>(
        IQueryable<TEntity> source,
        string stringPropertyName,
        string trimmedSearchText)
        where TEntity : class
    {
        var pattern = RelationalLikePattern.BuildContainsPattern(trimmedSearchText, sqlServerLike: false);
        const string escape = "\\";
        return source.Where(e =>
            EF.Functions.ILike(
                EF.Property<string>(e, stringPropertyName),
                pattern,
                escape));
    }
}
