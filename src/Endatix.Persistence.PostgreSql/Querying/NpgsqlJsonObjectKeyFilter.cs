using System.Linq.Expressions;
using Endatix.Infrastructure.Data.Querying;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Persistence.PostgreSql.Querying;

/// <summary>
/// PostgreSQL JSON object-key filter using ILIKE + <c>jsonb_extract_path_text</c>.
/// </summary>
public sealed class NpgsqlJsonObjectKeyFilter : RelationalJsonObjectKeyFilterBase
{
    /// <inheritdoc />
    protected override bool UsesSqlServerLikeSyntax => false;

    /// <inheritdoc />
    protected override Expression ExtractKeyText(Expression jsonProperty, string jsonObjectKey) =>
        Expression.Call(
            NpgsqlJsonDbFunctions.ExtractObjectKeyTextMethod,
            jsonProperty,
            Expression.Constant(jsonObjectKey));

    /// <inheritdoc />
    protected override Expression MatchesPattern(Expression text, string pattern) =>
        Expression.Call(
            typeof(NpgsqlDbFunctionsExtensions),
            nameof(NpgsqlDbFunctionsExtensions.ILike),
            typeArguments: Type.EmptyTypes,
            Expression.Constant(EF.Functions),
            text,
            Expression.Constant(pattern),
            Expression.Constant("\\"));
}
