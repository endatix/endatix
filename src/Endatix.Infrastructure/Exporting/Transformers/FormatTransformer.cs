using Endatix.Core.Abstractions.Exporting;

namespace Endatix.Infrastructure.Exporting.Transformers;

/// <summary>
/// Wraps a Func to format a value as string. Used for ExportOptions custom formatters.
/// </summary>
internal sealed class FormatTransformer : IValueTransformer
{
    private readonly Func<object?, string> _format;

    public FormatTransformer(Func<object?, string> format)
    {
        _format = format ?? throw new ArgumentNullException(nameof(format));
    }

    /// <inheritdoc />
    public object? Transform<T>(object? value, TransformationContext<T> context) => _format(value);
}
