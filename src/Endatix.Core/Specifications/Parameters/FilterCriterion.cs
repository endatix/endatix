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
/// Multiple values can be specified using comma separation.
/// Example: "age&gt;:18" or "status:active,pending"
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

        var @operator = _operatorMap.Keys
            .FirstOrDefault(op => 
            {
                var operatorIndex = filterExpression.IndexOf(op);
                if (operatorIndex == -1)
                {
                    return false;
                }

                // Check if all characters before the operator are letters or digits so they can be a valid field name
                return operatorIndex == 0 || filterExpression[..operatorIndex].All(c => char.IsLetterOrDigit(c));
            });

        Guard.Against.Null(
            @operator,
            nameof(filterExpression),
            $"Invalid filter operator. Valid operators are: {string.Join(", ", _operatorMap.Keys)}");

        var parts = filterExpression.Split(@operator, 2);
        Field = Guard.Against.NullOrWhiteSpace(
            parts[0].Trim(),
            nameof(filterExpression),
            "Filter must have a field");
        
        Operator = _operatorMap[@operator];

        var values = parts[1].Split(',').Select(v => v.Trim()).ToList();
        Guard.Against.InvalidInput(
            values,
            nameof(filterExpression),
            v => !v.Any(string.IsNullOrWhiteSpace),
            "Filter values cannot be empty or whitespace");

        Values = values.AsReadOnly();
    }

    /// <summary>
    /// Creates a new FilterCriterion by parsing the provided filter expression.
    /// </summary>
    /// <param name="filterExpression">The filter expression in the format "field[operator]value".</param>
    /// <returns>A new instance of FilterCriterion.</returns>
    public static FilterCriterion Parse(string filterExpression) => new FilterCriterion(filterExpression);
}
