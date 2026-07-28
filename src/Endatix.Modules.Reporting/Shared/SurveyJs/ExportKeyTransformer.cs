namespace Endatix.Modules.Reporting.Shared.SurveyJs;

/// <summary>
/// Transforms canonical export keys for output using a configurable segment delimiter.
/// </summary>
internal static class ExportKeyTransformer
{
    /// <summary>
    /// Trims whitespace on each path segment so Crunch/Shoji aliases and CSV headers
    /// do not retain trailing spaces/tabs from SurveyJS choice values.
    /// </summary>
    internal static string Sanitize(string canonicalKey)
    {
        if (string.IsNullOrEmpty(canonicalKey))
        {
            return canonicalKey;
        }

        if (!canonicalKey.Contains(ExportPathBuilder.SEGMENT_DELIMITER, StringComparison.Ordinal))
        {
            return canonicalKey.Trim();
        }

        var segments = canonicalKey.Split(ExportPathBuilder.SEGMENT_DELIMITER);
        for (var i = 0; i < segments.Length; i++)
        {
            segments[i] = segments[i].Trim();
        }

        return string.Join(ExportPathBuilder.SEGMENT_DELIMITER, segments);
    }

    internal static string Transform(string canonicalKey, string keySeparator)
    {
        var sanitized = Sanitize(canonicalKey);
        if (string.IsNullOrEmpty(sanitized) ||
            string.Equals(keySeparator, ExportPathBuilder.SEGMENT_DELIMITER, StringComparison.Ordinal))
        {
            return sanitized;
        }

        return sanitized.Replace(
            ExportPathBuilder.SEGMENT_DELIMITER,
            keySeparator,
            StringComparison.Ordinal);
    }

    internal static string RemoveLastSegment(string canonicalKey)
    {
        var sanitized = Sanitize(canonicalKey);
        var separatorIndex = sanitized.LastIndexOf(
            ExportPathBuilder.SEGMENT_DELIMITER,
            StringComparison.Ordinal);

        return separatorIndex < 0 ? sanitized : sanitized[..separatorIndex];
    }
}
