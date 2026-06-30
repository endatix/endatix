namespace Endatix.Modules.Reporting.Domain.SurveyJs;

internal static class SurveyJsElementTypes
{
    internal static readonly HashSet<string> NonDataTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "html",
        "expression",
        "image",
    };

    internal static readonly HashSet<string> ContainerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "panel",
        "paneldynamic",
    };

    internal static readonly HashSet<string> ChoiceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "checkbox",
        "radiogroup",
        "dropdown",
        "tagbox",
    };

    internal static readonly HashSet<string> MatrixTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "matrix",
        "matrixdropdown",
        "matrixdynamic",
    };

    internal static bool IsNonData(string? type) =>
        type is not null && NonDataTypes.Contains(type);

    internal static bool IsDrivingChoiceType(string? type) =>
        type is not null && (string.Equals(type, "checkbox", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(type, "radiogroup", StringComparison.OrdinalIgnoreCase));
}
