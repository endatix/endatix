namespace Endatix.Modules.Reporting.Shared.SurveyJs;

/// <summary>
/// Transforms canonical export keys for output using a configurable segment delimiter.
/// </summary>
internal static class ExportKeyTransformer
{
    internal static string Transform(string canonicalKey, string keySeparator)
    {
        if (string.IsNullOrEmpty(canonicalKey) ||
            string.Equals(keySeparator, ExportPathBuilder.SEGMENT_DELIMITER, StringComparison.Ordinal))
        {
            return canonicalKey;
        }

        return canonicalKey.Replace(
            ExportPathBuilder.SEGMENT_DELIMITER,
            keySeparator,
            StringComparison.Ordinal);
    }

    internal static string RemoveLastSegment(string canonicalKey)
    {
        var separatorIndex = canonicalKey.LastIndexOf(
            ExportPathBuilder.SEGMENT_DELIMITER,
            StringComparison.Ordinal);

        return separatorIndex < 0 ? canonicalKey : canonicalKey[..separatorIndex];
    }
}
