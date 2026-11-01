using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Endatix.Infrastructure.Data.Querying;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Persistence.PostgreSql.Querying;

/// <summary>
/// PostgreSQL JSON object-key filter using ILIKE + <c>jsonb_extract_path_text</c>.
/// </summary>
public sealed class NpgsqlJsonObjectKeyFilter : RelationalJsonObjectKeyFilterBase
{
    private const string IsoDatePrefix = "^[0-9]{4}-[0-9]{2}-[0-9]{2}";

    private static readonly MethodInfo IsMatchMethod =
        typeof(Regex).GetMethod(nameof(Regex.IsMatch), [typeof(string), typeof(string)])!;

    /// <inheritdoc />
    protected override bool UsesSqlServerLikeSyntax => false;

    /// <inheritdoc />
    protected override string OrdinalCollation => "C";

    /// <inheritdoc />
    protected override Expression ExtractKeyText(Expression jsonProperty, string jsonObjectKey) =>
        Expression.Call(
            NpgsqlJsonDbFunctions.ExtractObjectKeyTextMethod,
            jsonProperty,
            Expression.Constant(jsonObjectKey));

    /// <inheritdoc />
    protected override Expression MatchesPattern(Expression text, Expression lowercasedPattern) =>
        Expression.Call(
            typeof(NpgsqlDbFunctionsExtensions),
            nameof(NpgsqlDbFunctionsExtensions.ILike),
            typeArguments: Type.EmptyTypes,
            Expression.Constant(EF.Functions),
            text,
            lowercasedPattern,
            Expression.Constant("\\"));

    /// <inheritdoc />
    protected override Expression StartsWithIsoDate(Expression ordinalText) =>
        Expression.Call(IsMatchMethod, ordinalText, Expression.Constant(IsoDatePrefix));
}
