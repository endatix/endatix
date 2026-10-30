using System.Reflection;

namespace Endatix.Infrastructure.Data.Querying;

/// <summary>
/// Provider-neutral CLR stubs for JSON columns, translated by each provider's Endatix model customizer. A static
/// function, unlike <see cref="IRelationalJsonObjectKeyFilter"/>, can be written inside a Specification's expression,
/// which cannot take an injected service.
/// </summary>
public static class JsonDbFunctions
{
    /// <summary>Compile-time-safe method handle the provider model customizers translate.</summary>
    public static readonly MethodInfo KeyTextMethod =
        typeof(JsonDbFunctions).GetMethod(nameof(KeyText), [typeof(string), typeof(string)])!;

    /// <summary>
    /// The scalar value of a top-level key of a JSON object column, as text: <c>jsonb_extract_path_text</c> on
    /// PostgreSQL, <c>JSON_VALUE</c> on SQL Server. Null when the column or the key is missing. A number reads as its
    /// digits, so an exact comparison to an id is an exact JSON lookup. <paramref name="key"/> must be a constant.
    /// </summary>
    public static string? KeyText(string? json, string key)
        => throw new InvalidOperationException(
            $"{nameof(KeyText)} is a database function and cannot be evaluated on the client.");
}
