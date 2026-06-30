namespace Endatix.Modules.Reporting.Domain.SurveyJs;

internal static class ExportPathBuilder
{
    internal const char SegmentSeparator = '_';
    internal const string SegmentDelimiter = "__";

    internal static string Join(params ReadOnlySpan<string> segments)
    {
        if (segments.Length == 0)
        {
            return string.Empty;
        }

        if (segments.Length == 1)
        {
            return segments[0];
        }

        return string.Join(SegmentDelimiter, segments.ToArray());
    }

    internal static string ChoiceKey(string questionName, string choiceValue) =>
        Join(questionName, choiceValue);

    internal static string CheckboxChoiceKey(string questionName, string choiceValue) =>
        ChoiceKey(questionName, choiceValue);

    internal static string RankingChoiceKey(string questionName, string choiceValue) =>
        ChoiceKey(questionName, choiceValue);

    internal static string CheckboxOtherTextKey(string questionName) =>
        Join(questionName, "other_text");

    internal static string PanelIndexKey(string panelName, int index, string questionName) =>
        Join(panelName, index.ToString(System.Globalization.CultureInfo.InvariantCulture), questionName);

    internal static string NestedLoopKey(IReadOnlyList<string> choiceValues, string questionName) =>
        Join([.. choiceValues, questionName]);

    internal static string MatrixRowKey(string matrixName, string rowValue) =>
        Join(matrixName, rowValue);
}
