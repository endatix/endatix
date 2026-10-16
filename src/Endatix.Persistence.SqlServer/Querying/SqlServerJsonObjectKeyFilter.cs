using System.Linq.Expressions;
using System.Reflection;
using Endatix.Infrastructure.Data.Querying;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Persistence.SqlServer.Querying;

/// <summary>
/// SQL Server JSON object-key filter using LIKE + <c>JSON_VALUE … RETURNING nvarchar(max)</c>.
/// </summary>
/// <remarks>
/// LIKE follows the database collation, which may be case-sensitive, so a match compares the lowercased value with
/// the lowercased pattern.
/// </remarks>
public sealed class SqlServerJsonObjectKeyFilter : RelationalJsonObjectKeyFilterBase
{
    private static readonly MethodInfo ToLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;

    /// <inheritdoc />
    protected override bool UsesSqlServerLikeSyntax => true;

    /// <inheritdoc />
    protected override string OrdinalCollation => "Latin1_General_100_BIN2";

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
    protected override Expression MatchesPattern(Expression text, Expression lowercasedPattern) =>
        Expression.Call(
            typeof(DbFunctionsExtensions),
            nameof(DbFunctionsExtensions.Like),
            typeArguments: Type.EmptyTypes,
            Expression.Constant(EF.Functions),
            Expression.Call(text, ToLowerMethod),
            lowercasedPattern,
            Expression.Constant("\\"));
}
