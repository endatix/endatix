using System.Reflection;

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
            [typeof(string), typeof(string)]);

    /// <summary>SPIKE: JSON_VALUE ... RETURNING nvarchar(max).</summary>
    public static readonly MethodInfo JsonValueMaxMethod =
        typeof(SqlServerJsonDbFunctions).GetMethod(nameof(JsonValueMax), [typeof(string), typeof(string)]);

    /// <summary>SPIKE: JSON_VALUE(json, path RETURNING nvarchar(max)); not limited to 4,000 characters.</summary>
    public static string? JsonValueMax(string? json, string path)
        => throw new InvalidOperationException("database function");

    /// <summary>
    /// Extracts a JSON scalar by path (<c>JSON_VALUE</c>). Pass a full path such as <c>$.default</c>.
    /// </summary>
    public static string? JsonValue(string? json, string path)
        => throw new InvalidOperationException(
            $"{nameof(JsonValue)} is a database function and cannot be evaluated on the client.");
}
