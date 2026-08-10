using Endatix.Core.Common.Translations;
using FluentValidation;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Shared validation for the public <c>includeLocales</c> query parameter
/// and management <c>ensureLocales</c> import option.
/// </summary>
internal static class IncludeLocalesRules
{
    /// <summary>
    /// Upper bound on how many locales one request may ask for.
    /// </summary>
    internal const int MaxLocales = IHasTranslations.DEFAULT_MAX_AVAILABLE_CULTURES;

    /// <summary>
    /// Accepts repeated or comma-separated culture codes, bounded by <see cref="MaxLocales"/>.
    /// </summary>
    internal static IRuleBuilderOptions<T, IReadOnlyCollection<string>> IsIncludeLocales<T>(
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
    internal static IRuleBuilderOptions<T, IReadOnlyCollection<string>> IsEnsureLocales<T>(
        this IRuleBuilder<T, IReadOnlyCollection<string>> ruleBuilder) =>
        ruleBuilder
            .Must(locales => TranslationLocaleList.Tokenize(locales).Take(MaxLocales + 1).Count() <= MaxLocales)
            .WithMessage($"No more than {MaxLocales} locales can be ensured.")
            .Must(locales => TranslationLocaleList.Tokenize(locales).All(token =>
                CultureCode.TryParse(token, out var culture) && !culture.IsSyntheticDefault))
            .WithMessage("Each ensureLocales value must be a valid culture code (e.g. 'es'), not 'default'.");
}
