using System.Linq.Expressions;
using Ardalis.GuardClauses;

namespace Endatix.Core.Specifications.Parameters;

/// <summary>
/// Represents a filter criterion that can be used to build query specifications.
/// This class parses and stores field-based filter expressions in the format "field[operator]value".
/// </summary>
/// <remarks>
/// Supported operators:
/// - ":" for equality
/// - "!:" for inequality
/// - "&gt;" for greater than
/// - "&lt;" for less than
/// - "&gt;:" for greater than or equal
/// - "&lt;:" for less than or equal
/// 
/// Multiple values can be specified using pipe separation.
/// Example: "age&gt;:18" or "status:active|pending"
/// </remarks>
public class FilterCriterion
{
    public string Field { get; private set; }
    public ExpressionType Operator { get; private set; }
    public IReadOnlyList<string> Values { get; private set; }

    private static readonly Dictionary<string, ExpressionType> _operatorMap = new()
    {
        ["!:"] = ExpressionType.NotEqual,
        [">:"] = ExpressionType.GreaterThanOrEqual,
        ["<:"] = ExpressionType.LessThanOrEqual,
        [":" ] = ExpressionType.Equal,
        [">" ] = ExpressionType.GreaterThan,
        ["<" ] = ExpressionType.LessThan,
    };

    /// <summary>
    /// Initializes a new instance of the FilterCriterion class by parsing a filter expression.
    /// </summary>
    /// <param name="filterExpression">The filter expression in the format "field[operator]value".</param>
    /// <exception cref="ArgumentException">Thrown when the filter expression is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the filter expression is null or empty.</exception>
    public FilterCriterion(string filterExpression)
    {
        Guard.Against.NullOrWhiteSpace(filterExpression, nameof(filterExpression));

        var fieldEndIndex = 0;
        while (fieldEndIndex < filterExpression.Length && char.IsLetterOrDigit(filterExpression[fieldEndIndex]))
        {
            fieldEndIndex++;
        }

        Field = Guard.Against.NullOrWhiteSpace(
            filterExpression[..fieldEndIndex],
            nameof(filterExpression),
            "Filter must have a field name");

        var @operator = _operatorMap.Keys
            .FirstOrDefault(op => filterExpression.IndexOf(op, fieldEndIndex) == fieldEndIndex);

        Guard.Against.Null(
            @operator,
            nameof(filterExpression),
            $"Filter must have a valid operator after the field name. Valid operators are: {string.Join(", ", _operatorMap.Keys)}");

        Operator = _operatorMap[@operator];

        var valueStartIndex = fieldEndIndex + @operator.Length;
        var values = filterExpression[valueStartIndex..]
            .Split('|')
            .Select(v => v.Trim())
            .ToList();

        Guard.Against.InvalidInput(
            values,
            nameof(filterExpression),
            v => !v.Any(string.IsNullOrWhiteSpace),
            "Filter values cannot be empty");

        Values = values.AsReadOnly();
    }

    /// <summary>
    /// Creates a new FilterCriterion by parsing the provided filter expression.
    /// </summary>
    /// <param name="filterExpression">The filter expression in the format "field[operator]value".</param>
    /// <returns>A new instance of FilterCriterion.</returns>
    public static FilterCriterion Parse(string filterExpression) => new FilterCriterion(filterExpression);
}
