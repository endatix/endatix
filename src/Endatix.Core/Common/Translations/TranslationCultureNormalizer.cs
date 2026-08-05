using Ardalis.GuardClauses;

namespace Endatix.Core.Common.Translations;

/// <summary>
/// Normalizes culture codes used as SurveyJS translation keys (excluding the synthetic default key casing rules).
/// </summary>
public static class TranslationCultureNormalizer
{
    /// <summary>
    /// Trims and lowercases a culture code. Preserves <see cref="SurveyJsTranslationKeys.DefaultKey"/> exactly.
    /// </summary>
    public static string Normalize(string cultureCode)
    {
        Guard.Against.NullOrWhiteSpace(cultureCode);
        var trimmed = cultureCode.Trim();
        if (string.Equals(trimmed, SurveyJsTranslationKeys.DefaultKey, StringComparison.OrdinalIgnoreCase))
        {
            return SurveyJsTranslationKeys.DefaultKey;
        }

        return trimmed.ToLowerInvariant();
    }

    /// <summary>
    /// Returns whether <paramref name="cultureCode"/> is the synthetic SurveyJS default key (after trim).
    /// </summary>
    public static bool IsSyntheticDefaultKey(string? cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode))
        {
            return false;
        }

        return string.Equals(
            cultureCode.Trim(),
            SurveyJsTranslationKeys.DefaultKey,
            StringComparison.OrdinalIgnoreCase);
    }
}
