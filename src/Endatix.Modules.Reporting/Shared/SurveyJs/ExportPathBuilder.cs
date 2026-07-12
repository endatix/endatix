namespace Endatix.Modules.Reporting.Shared.SurveyJs;

internal static class ExportPathBuilder
{
    internal const char SEGMENT_SEPARATOR = '_';
    internal const string SEGMENT_DELIMITER = "__";

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

        return string.Join(SEGMENT_DELIMITER, segments.ToArray());
    }

    internal static string ChoiceKey(string questionName, string choiceValue) =>
        Join(questionName, choiceValue);

    internal static string ChoiceIndicatorKey(string questionName, string choiceValue) =>
        ChoiceKey(questionName, choiceValue);

    internal static string CheckboxChoiceKey(string questionName, string choiceValue) =>
        ChoiceIndicatorKey(questionName, choiceValue);

    internal static string RankingChoiceKey(string questionName, string choiceValue) =>
        ChoiceKey(questionName, choiceValue);

    internal static string ChoiceOtherTextKey(string questionName) =>
        Join(questionName, "other_text");

    internal static string CheckboxOtherTextKey(string questionName) =>
        ChoiceOtherTextKey(questionName);

    internal static string PanelIndexKey(string panelName, int index, string questionName) =>
        Join(panelName, index.ToString(System.Globalization.CultureInfo.InvariantCulture), questionName);

    internal static string NestedLoopKey(IReadOnlyList<string> choiceValues, string questionName) =>
        Join([.. choiceValues, questionName]);

    internal static string NestedLoopKey(ReadOnlySpan<string> choiceValues, string questionName)
    {
        if (choiceValues.IsEmpty)
        {
            return questionName;
        }

        var segments = new string[choiceValues.Length + 1];
        for (var i = 0; i < choiceValues.Length; i++)
        {
            segments[i] = choiceValues[i];
        }

        segments[^1] = questionName;
        return Join(segments);
    }

    internal static string MatrixRowKey(string matrixName, string rowValue) =>
        Join(matrixName, rowValue);

    internal static string MatrixCellKey(string matrixName, string rowSegment, string columnName) =>
        Join(matrixName, rowSegment, columnName);

    internal static string MultipleTextItemKey(string questionName, string itemName) =>
        Join(questionName, itemName);
}
