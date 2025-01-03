using FluentValidation;

namespace Endatix.Api.Common;

/// <summary>
/// Reusable Fluent validation for the IFilteredRequest implementations.
/// To use in your Validators add this to the validation <c>Include(new FilteredRequestValidator());</c>
/// </summary>
public class FilteredRequestValidator : AbstractValidator<IFilteredRequest>
{
    private static readonly string[] _validOperators = { "!:", ">:", "<:", "^:", ":", ">", "<" };
    private readonly Dictionary<string, Type>? _validFields;

    /// <summary>
    /// Default constructor
    /// </summary>
    public FilteredRequestValidator()
    {
        ConfigureRules();
    }

    /// <summary>
    /// Constructor that accepts a dictionary of valid field names and their corresponding types
    /// </summary>
    /// <param name="validFields">Dictionary of field names and their types that are allowed in filters</param>
    public FilteredRequestValidator(Dictionary<string, Type> validFields)
    {
        _validFields = new Dictionary<string, Type>(validFields, StringComparer.OrdinalIgnoreCase);
        ConfigureRules();
    }

    private void ConfigureRules()
    {
        RuleForEach(x => x.Filter)
            .Must((filter) => BeValidFilterFormat(filter, out var errorMessage))
            .WithMessage((_, filter) => GetFilterErrorMessage(filter));
    }

    private string GetFilterErrorMessage(string filter)
    {
        BeValidFilterFormat(filter, out var errorMessage);
        return $"Invalid filter '{filter}': {errorMessage}";
    }

    private bool BeValidFilterFormat(string filter, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(filter))
        {
            errorMessage = "Filter cannot be empty";
            return false;
        }

        var @operator = _validOperators.FirstOrDefault(op => filter.Contains(op));
        if (@operator == null)
        {
            errorMessage = $"Filter must contain one of these operators: {string.Join(", ", _validOperators)}";
            return false;
        }

        var parts = filter.Split(@operator, 2);
        if (parts.Length != 2 || 
            string.IsNullOrWhiteSpace(parts[0]) || 
            string.IsNullOrWhiteSpace(parts[1]))
        {
            errorMessage = "Filter must be in format 'field[operator]value'";
            return false;
        }

        if (_validFields != null)
        {
            if (!_validFields.ContainsKey(parts[0]))
            {
                errorMessage = $"Invalid field name '{parts[0]}'. Allowed fields: {string.Join(", ", _validFields.Keys)}";
                return false;
            }

            // For : and !: operators, check each value in the comma-separated list
            if (@operator is ":" or "!:")
            {
                var values = parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (!values.All(value => IsValidType(value.Trim(), _validFields[parts[0]])))
                {
                    errorMessage = $"One or more values are not valid for type {_validFields[parts[0]].Name}";
                    return false;
                }
            }
            else if (!IsValidType(parts[1], _validFields[parts[0]]))
            {
                errorMessage = $"Value is not valid for type {_validFields[parts[0]].Name}";
                return false;
            }
        }

        return true;
    }

    private bool IsValidType(string value, Type type) => 
        type switch
        {
            Type t when t == typeof(int) => int.TryParse(value, out _),
            Type t when t == typeof(long) => long.TryParse(value, out _),
            Type t when t == typeof(bool) => bool.TryParse(value, out _),
            Type t when t == typeof(DateTime) => DateTime.TryParse(value, out _),
            Type t when t == typeof(string) => true,
            _ => false
        };
}
