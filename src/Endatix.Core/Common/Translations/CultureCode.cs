using System.Text.RegularExpressions;
using Ardalis.GuardClauses;

namespace Endatix.Core.Common.Translations;

/// <summary>
/// Validated, normalized culture / SurveyJS translation key (open-ended tags plus synthetic <c>default</c>).
/// </summary>
public readonly partial record struct CultureCode
{
    /// <summary>
    /// Lowercase BCP-47-like tags: language (<c>en</c>, <c>fil</c>) with optional subtags (<c>en-us</c>, <c>zh-hans</c>).
    /// </summary>
    [GeneratedRegex("^[a-z]{2,3}(-[a-z0-9]{2,8})*$", RegexOptions.CultureInvariant)]
    private static partial Regex CultureCodePattern { get; }

    /// <summary>
    /// SurveyJS synthetic <c>default</c> key.
    /// </summary>
    public static CultureCode SyntheticDefault { get; } = new(SurveyJsTranslationKeys.DefaultKey);

    /// <summary>
    /// Already-normalized culture code or synthetic default key.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Whether this is the SurveyJS synthetic <c>default</c> key.
    /// </summary>
    public bool IsSyntheticDefault =>
        string.Equals(Value, SurveyJsTranslationKeys.DefaultKey, StringComparison.Ordinal);

    private CultureCode(string value) => Value = value;

    /// <summary>
    /// Parses and normalizes a culture code. Throws when invalid.
    /// </summary>
    public static CultureCode Parse(string cultureCode)
    {
        Guard.Against.NullOrWhiteSpace(cultureCode);
        if (!TryParse(cultureCode, out var code))
        {
            throw new ArgumentException($"'{cultureCode}' is not a valid culture code.", nameof(cultureCode));
        }

        return code;
    }

    /// <summary>
    /// Tries to parse and normalize a culture code without throwing.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when <paramref name="cultureCode"/> is the synthetic default key or a valid culture-code shaped tag
    /// within <see cref="IHasTranslations.MAX_CULTURE_CODE_LENGTH"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool TryParse(string? cultureCode, out CultureCode code)
    {
        code = default;
        if (string.IsNullOrWhiteSpace(cultureCode))
        {
            return false;
        }

        var trimmed = cultureCode.Trim();
        if (trimmed.Length > IHasTranslations.MAX_CULTURE_CODE_LENGTH)
        {
            return false;
        }

        if (string.Equals(trimmed, SurveyJsTranslationKeys.DefaultKey, StringComparison.OrdinalIgnoreCase))
        {
            code = SyntheticDefault;
            return true;
        }

        var candidate = trimmed.ToLowerInvariant();
        if (!CultureCodePattern.IsMatch(candidate))
        {
            return false;
        }

        code = new CultureCode(candidate);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
