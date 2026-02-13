using System.Text.Json;
using System.Text.Json.Nodes;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Exporting;

namespace Endatix.Infrastructure.Exporting.ColumnDefinitions;

/// <summary>
/// Represents an abstract base column definition for exporting data.
/// Extract, Pipe, Format: ExtractRawValue feeds a single pipeline of IValueTransformer.
/// </summary>
public abstract class ColumnDefinition<T> where T : class
{
    public string Name { get; }
    public string JsonPropertyName { get; set; }

    private IValueFormatter? _formatter;

    private readonly List<IValueTransformer> _transformers = new();

    protected ColumnDefinition(string name)
    {
        Name = name;
        JsonPropertyName = JsonNamingPolicy.CamelCase.ConvertName(name);
    }

    /// <summary>
    /// Adds a value transformer to the pipeline.
    /// </summary>
    public ColumnDefinition<T> AddTransformer(IValueTransformer transformer)
    {
        Guard.Against.Null(transformer);

        _transformers.Add(transformer);
        return this;
    }

    /// <summary>
    /// Sets a value formatter to the column.
    /// </summary>
    public ColumnDefinition<T> SetFormatter(IValueFormatter formatter)
    {
        Guard.Against.Null(formatter);

        _formatter = formatter;
        return this;
    }

    /// <summary>
    /// Hook for subclasses: returns the initial value (e.g. JsonElement or property value).
    /// </summary>
    protected abstract object? ExtractRawValue(T row, JsonDocument? document);


    /// <summary>
    /// Gets the value for export. Runs pipeline of all transformers and formatters. Use for JSON export.
    /// </summary>
    /// <param name="context">The context of the transformation.</param>
    /// <param name="applyFormatters">Whether to apply formatters.</param>
    /// <returns>The value for export.</returns>
    public object? GetValue(TransformationContext<T> context, bool applyFormatters = true)
    {
        var rawValue = ExtractRawValue(context.Row, context.JsonDoc);
        if (_transformers is { Count: 0 })
        {
            return applyFormatters ? GetFormattedValue(rawValue, context) : rawValue;
        }

        var node = JsonNodeParser.ToJsonNode(rawValue);
        if (node is null)
        {
            return rawValue;
        }

        var count = _transformers.Count;
        for (var i = 0; i < count; i++)
        {
            node = _transformers[i].Transform(node, context)!;
        }

        return applyFormatters ? GetFormattedValue(node, context) : node;
    }

    /// <summary>
    /// Gets the formatted value for export. Runs pipeline of all transformers and formatters. Use for JSON export.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <param name="context">The context of the transformation.</param>
    /// <returns>The formatted value.</returns>
    private object? GetFormattedValue(object? value, TransformationContext<T> context)
    {
        if (value is null)
        {
            return null;
        }

        if (_formatter is not null)
        {
            return _formatter.Format(value, context);
        }

        return value;
    }
}

/// <summary>
/// Column definition for accessing static properties of an entity.
/// </summary>
public sealed class StaticColumnDefinition<T> : ColumnDefinition<T> where T : class
{
    private readonly Func<T, object?> _accessor;

    public StaticColumnDefinition(string name, Func<T, object?> accessor) : base(name)
    {
        _accessor = accessor;
    }

    /// <inheritdoc />
    protected override object? ExtractRawValue(T row, JsonDocument? document) => _accessor(row);
}

/// <summary>
/// Column definition for accessing JSON properties. Extracts JsonElement into the pipeline (zero-copy start).
/// </summary>
public sealed class JsonColumnDefinition<T> : ColumnDefinition<T> where T : class
{
    private readonly string _jsonPath;

    public JsonColumnDefinition(string name, string jsonPath) : base(name)
    {
        _jsonPath = jsonPath;
    }

    /// <inheritdoc />
    protected override object? ExtractRawValue(T row, JsonDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        if (document.RootElement.TryGetProperty(_jsonPath, out var element))
        {
            return element;
        }

        return null;
    }
}
