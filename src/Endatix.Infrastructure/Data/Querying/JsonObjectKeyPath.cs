namespace Endatix.Infrastructure.Data.Querying;

/// <summary>
/// A top-level key of a JSON object column: the mapped property that holds the object, and the key in it.
/// </summary>
public readonly record struct JsonObjectKeyPath(string JsonPropertyName, string JsonObjectKey);
