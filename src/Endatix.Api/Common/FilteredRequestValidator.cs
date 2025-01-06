using FluentValidation;

namespace Endatix.Api.Common;

/// <summary>
/// Reusable Fluent validation for the IFilteredRequest implementations.
/// To use in your Validators add this to the validation <c>Include(new FilteredRequestValidator());</c>
/// </summary>
public class FilteredRequestValidator : AbstractValidator<IFilteredRequest>
{
    private static readonly string[] _validOperators = { "!:", ">:", "<:", ":", ">", "<" };
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

        // If we have valid fields defined, check if the filter starts with any of them
        if (_validFields != null)
        {
            var matchingField = _validFields.Keys
                .FirstOrDefault(field => 
                    filter.StartsWith(field, StringComparison.OrdinalIgnoreCase) && 
                    filter.Length > field.Length && 
                    !char.IsLetterOrDigit(filter[field.Length]));

            if (matchingField == null)
            {
                errorMessage = $"Filter must start with a valid field name. Allowed fields: {string.Join(", ", _validFields.Keys)}";
                return false;
            }

            // Extract the rest of the filter after the field name
            var remainingFilter = filter[matchingField.Length..];

            // Check if the remaining part starts with a valid operator
            var matchingOperator = _validOperators.FirstOrDefault(op => remainingFilter.StartsWith(op));
            if (matchingOperator == null)
            {
                errorMessage = $"After field name '{matchingField}', filter must contain one of these operators: {string.Join(", ", _validOperators)}";
                return false;
            }

            // Extract the value part
            var value = remainingFilter[matchingOperator.Length..];
            if (string.IsNullOrWhiteSpace(value))
            {
                errorMessage = "Filter must include a value after the operator";
                return false;
            }

            // Validate the value type
            if (matchingOperator is ":" or "!:")
            {
                var values = value.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (!values.All(v => IsValidType(v.Trim(), _validFields[matchingField])))
                {
                    errorMessage = $"One or more values are not valid for type {_validFields[matchingField].Name}";
                    return false;
                }
            }
            else if (!IsValidType(value, _validFields[matchingField]))
            {
                errorMessage = $"Value is not valid for type {_validFields[matchingField].Name}";
                return false;
            }
        }
        else
        {
            // If no valid fields are defined, just check for operator presence
            var hasValidOperator = _validOperators.Any(op => filter.Contains(op));
            if (!hasValidOperator)
            {
                errorMessage = $"Filter must contain one of these operators: {string.Join(", ", _validOperators)}";
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
