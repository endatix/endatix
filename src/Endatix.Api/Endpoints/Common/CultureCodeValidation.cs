using Endatix.Core.Common.Translations;
using FluentValidation;

namespace Endatix.Api.Endpoints.Common;

/// <summary>
/// Shared FluentValidation rules for culture / locale request fields.
/// </summary>
public static class CultureCodeValidation
{
    /// <summary>
    /// Upper bound on how many locales one request may ask for.
    /// </summary>
    public const int MaxLocales = IHasTranslations.DEFAULT_MAX_AVAILABLE_CULTURES;

    /// <summary>
    /// Requires a non-empty real culture code. Rejects the synthetic SurveyJS <c>default</c> key.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> IsCultureCode<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .NotEmpty()
            .Must(locale => CultureCode.TryParse(locale, out var culture) && !culture.IsSyntheticDefault)
            .WithMessage("{PropertyName} must be a valid culture code (e.g. 'es'), not 'default'.");

    /// <summary>
    /// Accepts a single culture or comma-separated list (e.g. <c>es,de</c>).
    /// Rejects the synthetic <c>default</c> key and caps token count at <see cref="MaxLocales"/>.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> IsHasLocaleFilter<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(BeValidHasLocaleFilter)
            .WithMessage(
                "{PropertyName} must be a culture code or comma-separated list (e.g. 'es' or 'es,de'), not 'default'.");

    private static bool BeValidHasLocaleFilter(string? hasLocale)
    {
        if (string.IsNullOrWhiteSpace(hasLocale))
        {
            return true;
        }

        List<string> tokens = TranslationLocaleList.Tokenize([hasLocale]).Take(MaxLocales + 1).ToList();
        if (tokens.Count == 0 || tokens.Count > MaxLocales)
        {
            return false;
        }

        foreach (string token in tokens)
        {
            if (!CultureCode.TryParse(token, out CultureCode culture) || culture.IsSyntheticDefault)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Accepts repeated or comma-separated culture codes, bounded by <see cref="MaxLocales"/>.
    /// Allows the synthetic <c>default</c> key.
    /// Uses rule-level <see cref="CascadeMode.Stop"/> so an oversized list never runs culture parsing
    /// (see FluentValidation cascade docs).
    /// </summary>
    public static IRuleBuilderOptions<T, IReadOnlyCollection<string>> IsIncludeLocales<T>(
        this IRuleBuilderInitial<T, IReadOnlyCollection<string>> ruleBuilder) =>
        ruleBuilder.MustBeBoundedCultureTokens(
            allowSyntheticDefault: true,
            tooManyMessage: $"No more than {MaxLocales} locales can be requested.",
            invalidTokenMessage: "Each locale must be a valid culture code (e.g. 'es' or 'en-US') or 'default'.");

    /// <summary>
    /// Same bounds as <see cref="IsIncludeLocales{T}"/> but rejects the synthetic <c>default</c> key
    /// (ensure locales are real cultures added to AvailableLocales).
    /// </summary>
    public static IRuleBuilderOptions<T, IReadOnlyCollection<string>> IsEnsureLocales<T>(
        this IRuleBuilderInitial<T, IReadOnlyCollection<string>> ruleBuilder) =>
        ruleBuilder.MustBeBoundedCultureTokens(
            allowSyntheticDefault: false,
            tooManyMessage: $"No more than {MaxLocales} locales can be ensured.",
            invalidTokenMessage: "Each ensureLocales value must be a valid culture code (e.g. 'es'), not 'default'.");

    /// <summary>
    /// Count-check first, then parse. <see cref="CascadeMode.Stop"/> skips parsing when the cap fails.
    /// </summary>
    private static IRuleBuilderOptions<T, IReadOnlyCollection<string>> MustBeBoundedCultureTokens<T>(
        this IRuleBuilderInitial<T, IReadOnlyCollection<string>> ruleBuilder,
        bool allowSyntheticDefault,
        string tooManyMessage,
        string invalidTokenMessage) =>
        ruleBuilder
            .Cascade(CascadeMode.Stop)
            .Must(locales => CountTokensAtMost(locales, MaxLocales))
            .WithMessage(tooManyMessage)
            .Must(locales => AreTokensValid(locales, allowSyntheticDefault))
            .WithMessage(invalidTokenMessage);

    private static bool CountTokensAtMost(IEnumerable<string>? locales, int maxCount) =>
        TranslationLocaleList.Tokenize(locales).Take(maxCount + 1).Count() <= maxCount;

    private static bool AreTokensValid(IEnumerable<string>? locales, bool allowSyntheticDefault)
    {
        // After a successful count check, tokens are ≤ MaxLocales; Take still bounds enumeration.
        foreach (var token in TranslationLocaleList.Tokenize(locales).Take(MaxLocales))
        {
            if (!CultureCode.TryParse(token, out var culture)
                || (!allowSyntheticDefault && culture.IsSyntheticDefault))
            {
                return false;
            }
        }

        return true;
    }
}
