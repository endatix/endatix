using Endatix.Infrastructure.Data.Querying;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Persistence.SqlServer.Querying;

/// <summary>
/// Case sensitivity follows column/database collation; pattern uses LIKE with ESCAPE.
/// </summary>
public sealed class SqlServerSubstringLikeFilter : IRelationalSubstringLikeFilter
{
    /// <inheritdoc />
    public IQueryable<TEntity> WherePropertyMatchesLikeSubstring<TEntity>(
        IQueryable<TEntity> source,
        string stringPropertyName,
        string trimmedSearchText)
        where TEntity : class
    {
        var pattern = RelationalLikePattern.BuildContainsPattern(trimmedSearchText, sqlServerLike: true);
        const string escape = "\\";
        return source.Where(e =>
            EF.Functions.Like(
                EF.Property<string>(e, stringPropertyName),
                pattern,
                escape));
    }
}
