using System.Reflection;

namespace Endatix.Persistence.PostgreSql.Querying;

/// <summary>
/// CLR stub for PostgreSQL <c>jsonb_extract_path_text</c>, mapped via the Endatix model customizer.
/// </summary>
public static class NpgsqlJsonDbFunctions
{
    /// <summary>
    /// Compile-time-safe method handle used by <see cref="NpgsqlEndatixModelCustomizer"/>.
    /// </summary>
    public static readonly MethodInfo ExtractObjectKeyTextMethod =
        typeof(NpgsqlJsonDbFunctions).GetMethod(
            nameof(ExtractObjectKeyText),
            [typeof(string), typeof(string)])!;

    /// <summary>
    /// Extracts a top-level JSON object string value by key (<c>jsonb_extract_path_text</c>).
    /// </summary>
    public static string? ExtractObjectKeyText(string? json, string key)
        => throw new InvalidOperationException(
            $"{nameof(ExtractObjectKeyText)} is a database function and cannot be evaluated on the client.");
}
