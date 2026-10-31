using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Features.SearchCopy;

namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// Checks answer filters against a form's search keys: each key must be one, and its filter type must support the
/// operator.
/// </summary>
internal static class AnswerFilterRules
{
    internal const string Field = "answer";
    internal const string UnknownKeyCode = "answer_key_not_filterable";
    internal const string UnsupportedOperatorCode = "answer_operator_not_supported";

    public static List<ValidationError> Check(IReadOnlyList<AnswerFilter> filters, FormSearchKeys searchKeys)
    {
        var filterTypes = searchKeys.Keys.ToDictionary(key => key.Key, key => key.FilterType, StringComparer.Ordinal);
        List<ValidationError> errors = [];
        foreach (var filter in filters)
        {
            if (!filterTypes.TryGetValue(filter.Key, out var filterType))
            {
                errors.Add(Error($"Column '{filter.Key}' cannot be filtered on this form.", UnknownKeyCode));
            }
            else if (!AnswerFilterOperators.Supports(filterType, filter.Operator))
            {
                errors.Add(Error(
                    $"Column '{filter.Key}' is a {filterType} column and does not support '{filter.Operator}'.",
                    UnsupportedOperatorCode));
            }
        }

        return errors;
    }

    private static ValidationError Error(string message, string code) =>
        new(Field, message, code, ValidationSeverity.Error);
}
