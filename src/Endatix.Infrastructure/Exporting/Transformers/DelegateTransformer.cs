using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Exporting;

namespace Endatix.Infrastructure.Exporting.Transformers;

/// <summary>
/// Transforms a value using a delegate.
/// </summary>
public sealed class DelegateTransformer : IValueTransformer
{
    private readonly Func<object?, string> _transform;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateTransformer"/> class.
    /// </summary>
    /// <param name="transform">The delegate to transform the value. Func that takes an object and returns a string.</param>
    public DelegateTransformer(Func<object?, string> transform)
    {
        Guard.Against.Null(transform);

        _transform = transform;
    }

    public object? Transform<T>(object? value, TransformationContext<T> context) => _transform(value);
}
