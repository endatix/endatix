namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Transforms a value in the export pipeline. Return null when no change.
/// </summary>
public interface IValueTransformer
{
    /// <summary>
    /// Transforms the input value. Returns null when no change.
    /// </summary>
    object? Transform(object? value);
}
