using System.Reflection;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Endatix.Persistence.SqlServer.Querying;

/// <summary>
/// CLR stub for SQL Server <c>JSON_VALUE</c>, mapped via the Endatix model customizer.
/// </summary>
public static class SqlServerJsonDbFunctions
{
    /// <summary>
    /// Compile-time-safe method handle used by <see cref="SqlServerEndatixModelCustomizer"/>.
    /// </summary>
    public static readonly MethodInfo JsonValueMethod =
        typeof(SqlServerJsonDbFunctions).GetMethod(
            nameof(JsonValue),
            [typeof(string), typeof(string)])!;

    /// <summary>
    /// Extracts a JSON scalar by path (<c>JSON_VALUE</c>). Pass a full path such as <c>$.default</c>. A constant path
    /// reads the whole value, whatever its length.
    /// </summary>
    public static string? JsonValue(string? json, string path)
        => throw new InvalidOperationException(
            $"{nameof(JsonValue)} is a database function and cannot be evaluated on the client.");

    /// <summary>
    /// Translates <see cref="JsonValue"/>. Lax <c>JSON_VALUE</c> returns NULL for a value over 4,000 characters, so a
    /// constant path reads the value <c>RETURNING nvarchar(max)</c>. EF has no expression for that clause, so the path
    /// and the clause are written as one fragment, with the path's quotes doubled.
    /// </summary>
    internal static SqlExpression Translate(IReadOnlyList<SqlExpression> arguments)
    {
        var path = arguments[1];
        SqlExpression pathArgument = path is SqlConstantExpression { Value: string constantPath }
            ? new SqlFragmentExpression($"N'{constantPath.Replace("'", "''", StringComparison.Ordinal)}' RETURNING nvarchar(max)")
            : path;

        return new SqlFunctionExpression(
            "JSON_VALUE",
            [arguments[0], pathArgument],
            nullable: true,
            argumentsPropagateNullability: [true, false],
            typeof(string),
            path.TypeMapping);
    }
}
