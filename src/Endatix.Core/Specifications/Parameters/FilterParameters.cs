using Ardalis.GuardClauses;

namespace Endatix.Core.Specifications.Parameters;

/// <summary>
/// Represents a collection of filter criteria used for building specifications.
/// This class manages a list of <see cref="FilterCriterion"/> objects that define
/// filtering conditions for queries.
/// </summary>
public class FilterParameters
{
    private readonly List<FilterCriterion> _criteria;

    public IReadOnlyList<FilterCriterion> Criteria => _criteria.AsReadOnly();

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterParameters"/> class.
    /// </summary>
    public FilterParameters()
    {
        _criteria = new List<FilterCriterion>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterParameters"/> class with the specified filter expressions.
    /// </summary>
    /// <param name="filterExpressions">The collection of filter expressions to initialize with.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filterExpressions"/> is null.</exception>
    public FilterParameters(IEnumerable<string> filterExpressions)
    {
        if (filterExpressions != null && filterExpressions.Any())
        {
            _criteria = filterExpressions
                .Select(FilterCriterion.Parse)
                .ToList();
        }
        else
        {
            _criteria = [];
        }
    }

    /// <summary>
    /// Adds a new filter criterion parsed from the specified filter expression.
    /// </summary>
    /// <param name="filterExpression">The filter expression to parse and add.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filterExpression"/> is null or whitespace.</exception>
    public void AddFilter(string filterExpression)
    {
        Guard.Against.NullOrWhiteSpace(filterExpression, nameof(filterExpression));
        
        _criteria.Add(FilterCriterion.Parse(filterExpression));
    }

    /// <summary>
    /// Adds the specified filter criterion to the collection.
    /// </summary>
    /// <param name="criterion">The filter criterion to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="criterion"/> is null.</exception>
    public void AddFilter(FilterCriterion criterion)
    {
        Guard.Against.Null(criterion, nameof(criterion));
        
        _criteria.Add(criterion);
    }

    /// <summary>
    /// Removes all filter criteria from the collection.
    /// </summary>
    public void Clear()
    {
        _criteria.Clear();
    }
}
