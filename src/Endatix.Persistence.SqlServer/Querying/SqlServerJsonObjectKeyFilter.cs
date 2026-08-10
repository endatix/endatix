using System.Linq.Expressions;
using Endatix.Infrastructure.Data.Querying;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Persistence.SqlServer.Querying;

/// <summary>
/// SQL Server JSON object-key filter using LIKE + <c>JSON_VALUE</c>.
/// </summary>
public sealed class SqlServerJsonObjectKeyFilter : RelationalJsonObjectKeyFilterBase
{
    /// <inheritdoc />
    protected override bool UsesSqlServerLikeSyntax => true;

    /// <summary>
    /// Builds a SQL Server JSON path. Always quotes the key so hyphenated cultures (e.g. <c>en-US</c>) are valid.
    /// </summary>
    public static string BuildJsonValuePath(string jsonObjectKey)
    {
        string escaped = jsonObjectKey
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"$.\"{escaped}\"";
    }

    /// <inheritdoc />
    protected override Expression ExtractKeyText(Expression jsonProperty, string jsonObjectKey) =>
        Expression.Call(
            SqlServerJsonDbFunctions.JsonValueMethod,
            jsonProperty,
            Expression.Constant(BuildJsonValuePath(jsonObjectKey)));

    /// <inheritdoc />
    protected override Expression MatchesPattern(Expression text, string pattern) =>
        Expression.Call(
            typeof(DbFunctionsExtensions),
            nameof(DbFunctionsExtensions.Like),
            typeArguments: Type.EmptyTypes,
            Expression.Constant(EF.Functions),
            text,
            Expression.Constant(pattern),
            Expression.Constant("\\"));
}
