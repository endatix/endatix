namespace Endatix.Core.Common.Translations;

/// <summary>
/// Parses wire locale lists (repeated or comma-separated query values) into <see cref="CultureCode"/> values.
/// </summary>
public static class TranslationLocaleList
{
    /// <summary>
    /// Splits raw wire values on commas and trims them, keeping the original casing.
    /// </summary>
    public static IEnumerable<string> Tokenize(IEnumerable<string>? locales)
    {
        if (locales is null)
        {
            yield break;
        }

        foreach (var raw in locales)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return token;
            }
        }
    }

    /// <summary>
    /// Tokenizes and parses locales, dropping malformed codes, de-duplicating, and capping the result.
    /// </summary>
    public static IReadOnlyList<CultureCode> ParseMany(
        IEnumerable<string>? locales,
        int maxCount = IHasTranslations.DEFAULT_MAX_AVAILABLE_CULTURES)
    {
        List<CultureCode> parsed = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (var token in Tokenize(locales))
        {
            if (parsed.Count >= maxCount)
            {
                break;
            }

            if (!CultureCode.TryParse(token, out CultureCode code))
            {
                continue;
            }

            if (seen.Add(code.Value))
            {
                parsed.Add(code);
            }
        }

        return parsed;
    }
}
