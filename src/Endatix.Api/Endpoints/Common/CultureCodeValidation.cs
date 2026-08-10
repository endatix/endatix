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
            .Must(locale => CultureCode.TryParse(locale, out CultureCode culture) && !culture.IsSyntheticDefault)
            .WithMessage("{PropertyName} must be a valid culture code (e.g. 'es'), not 'default'.");

    /// <summary>
    /// Accepts repeated or comma-separated culture codes, bounded by <see cref="MaxLocales"/>.
    /// Allows the synthetic <c>default</c> key.
    /// </summary>
    public static IRuleBuilderOptions<T, IReadOnlyCollection<string>> IsIncludeLocales<T>(
        this IRuleBuilder<T, IReadOnlyCollection<string>> ruleBuilder) =>
        ruleBuilder
            .Must(locales => TranslationLocaleList.Tokenize(locales).Take(MaxLocales + 1).Count() <= MaxLocales)
            .WithMessage($"No more than {MaxLocales} locales can be requested.")
            .Must(locales => TranslationLocaleList.Tokenize(locales).All(token => CultureCode.TryParse(token, out _)))
            .WithMessage("Each locale must be a valid culture code (e.g. 'es' or 'en-US') or 'default'.");

    /// <summary>
    /// Same bounds as <see cref="IsIncludeLocales{T}"/> but rejects the synthetic <c>default</c> key
    /// (ensure locales are real cultures added to AvailableLocales).
    /// </summary>
    public static IRuleBuilderOptions<T, IReadOnlyCollection<string>> IsEnsureLocales<T>(
        this IRuleBuilder<T, IReadOnlyCollection<string>> ruleBuilder) =>
        ruleBuilder
            .Must(locales => TranslationLocaleList.Tokenize(locales).Take(MaxLocales + 1).Count() <= MaxLocales)
            .WithMessage($"No more than {MaxLocales} locales can be ensured.")
            .Must(locales => TranslationLocaleList.Tokenize(locales).All(token =>
                CultureCode.TryParse(token, out CultureCode culture) && !culture.IsSyntheticDefault))
            .WithMessage("Each ensureLocales value must be a valid culture code (e.g. 'es'), not 'default'.");
}
