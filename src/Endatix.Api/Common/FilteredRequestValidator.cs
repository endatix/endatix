using FluentValidation;

namespace Endatix.Api.Common;

/// <summary>
/// Reusable Fluent validation for the IFilteredRequest implementations.
/// To use in your Validators add this to the validation <c>Include(new FilteredRequestValidator());</c>
/// </summary>
public class FilteredRequestValidator : AbstractValidator<IFilteredRequest>
{
    private static readonly string[] _validOperators = { "!:", ">:", "<:", ":", ">", "<" };
    private readonly Dictionary<string, Type> _validFields;

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

        var field = _validFields.Keys
            .FirstOrDefault(field => 
                filter.StartsWith(field, StringComparison.OrdinalIgnoreCase) && 
                filter.Length > field.Length && 
                !char.IsLetterOrDigit(filter[field.Length]));

        if (field == null)
        {
            errorMessage = $"Filter must start with a valid field name. Allowed fields: {string.Join(", ", _validFields.Keys)}";
            return false;
        }

        var remainingFilter = filter[field.Length..];

        var @operator = _validOperators.FirstOrDefault(remainingFilter.StartsWith);
        if (@operator == null)
        {
            errorMessage = $"Filter must contain a valid operator after the field name. Allowed operators: {string.Join(", ", _validOperators)}";
            return false;
        }

        var value = remainingFilter[@operator.Length..];
        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = "Filter must contain a value after the operator.";
            return false;
        }

        // For : and !: operators, check each value in the comma-separated list
        if (@operator is ":" or "!:")
        {
            var values = value.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (!values.All(v => IsValidType(v.Trim(), _validFields[field])))
            {
                errorMessage = $"One or more values are not valid for type {_validFields[field].Name}";
                return false;
            }
        }
        else if (!IsValidType(value.Trim(), _validFields[field]))
        {
            errorMessage = $"Value is not valid for type {_validFields[field].Name}";
            return false;
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
