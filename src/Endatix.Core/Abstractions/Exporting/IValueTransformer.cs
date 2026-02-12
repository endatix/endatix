namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Transforms a value in the export pipeline. Input can be JsonElement, string, etc.
/// Return the same value if no change.
/// </summary>
public interface IValueTransformer
{
    /// <summary>
    /// Transforms the value. Returns the same value if no change.
    /// </summary>
    object? Transform<T>(object? value, TransformationContext<T> context);
}
