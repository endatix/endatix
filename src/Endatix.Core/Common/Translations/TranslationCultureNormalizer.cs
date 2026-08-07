using System.Text.RegularExpressions;
using Ardalis.GuardClauses;

namespace Endatix.Core.Common.Translations;

/// <summary>
/// Normalizes culture codes used as SurveyJS translation keys (excluding the synthetic default key casing rules).
/// </summary>
public static partial class TranslationCultureNormalizer
{
    /// <summary>
    /// Lowercase BCP-47-like tags: language (<c>en</c>, <c>fil</c>) with optional subtags (<c>en-us</c>, <c>zh-hans</c>).
    /// </summary>
    [GeneratedRegex("^[a-z]{2,3}(-[a-z0-9]{2,8})*$", RegexOptions.CultureInvariant)]
    private static partial Regex CultureCodePattern { get; }

    /// <summary>
    /// Trims and lowercases a culture code. Preserves <see cref="SurveyJsTranslationKeys.DefaultKey"/> exactly.
    /// Rejects values that are not valid culture-code shaped tags.
    /// </summary>
    public static string Normalize(string cultureCode)
    {
        Guard.Against.NullOrWhiteSpace(cultureCode);
        var trimmed = cultureCode.Trim();
        if (string.Equals(trimmed, SurveyJsTranslationKeys.DefaultKey, StringComparison.OrdinalIgnoreCase))
        {
            return SurveyJsTranslationKeys.DefaultKey;
        }

        var normalized = trimmed.ToLowerInvariant();
        if (!CultureCodePattern.IsMatch(normalized))
        {
            throw new ArgumentException($"'{cultureCode}' is not a valid culture code.", nameof(cultureCode));
        }

        return normalized;
    }

    /// <summary>
    /// Safely returns whether <paramref name="cultureCode"/> is a valid culture-code shaped tag or the synthetic default key
    /// (without throwing). Whitespace / null is invalid.
    /// </summary>
    public static bool IsValidCultureCode(string? cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode))
        {
            return false;
        }

        if (IsSyntheticDefaultKey(cultureCode))
        {
            return true;
        }

        return CultureCodePattern.IsMatch(cultureCode.Trim().ToLowerInvariant());
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
