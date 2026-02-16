namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Presentation Formatter: Formats a value for final output (e.g. Date -> String).
/// Operates on primitives or JsonElement directly. No JsonNode lifting required.
/// </summary>
public interface IValueFormatter
{
    object? Format<T>(object? value, TransformationContext<T> context);
}