using Endatix.Core.Abstractions.Exporting;

namespace Endatix.Infrastructure.Exporting.Formatters;

/// <summary>
/// Formats a value using a delegate.
/// </summary>
public class DelegateFormatter : IValueFormatter
{
    private readonly Func<object?, string> _delegateFunction;
    public DelegateFormatter(Func<object?, string> func) => _delegateFunction = func;

    public object? Format<T>(object? value, TransformationContext<T> context)
    {
        return _delegateFunction(value);
    }
}