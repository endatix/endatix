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
    /// Accepts repeated or comma-separated culture codes, bounded by <see cref="MaxLocales"/>.
    /// Allows the synthetic <c>default</c> key.
    /// </summary>
    public static IRuleBuilderOptionsConditions<T, IReadOnlyCollection<string>> IsIncludeLocales<T>(
        this IRuleBuilder<T, IReadOnlyCollection<string>> ruleBuilder) =>
        ruleBuilder.MustBeBoundedCultureTokens(
            allowSyntheticDefault: true,
            tooManyMessage: $"No more than {MaxLocales} locales can be requested.",
            invalidTokenMessage: "Each locale must be a valid culture code (e.g. 'es' or 'en-US') or 'default'.");

    /// <summary>
    /// Same bounds as <see cref="IsIncludeLocales{T}"/> but rejects the synthetic <c>default</c> key
    /// (ensure locales are real cultures added to AvailableLocales).
    /// </summary>
    public static IRuleBuilderOptionsConditions<T, IReadOnlyCollection<string>> IsEnsureLocales<T>(
        this IRuleBuilder<T, IReadOnlyCollection<string>> ruleBuilder) =>
        ruleBuilder.MustBeBoundedCultureTokens(
            allowSyntheticDefault: false,
            tooManyMessage: $"No more than {MaxLocales} locales can be ensured.",
            invalidTokenMessage: "Each ensureLocales value must be a valid culture code (e.g. 'es'), not 'default'.");

    /// <summary>
    /// Tokenizes once, fails immediately when the token count exceeds <see cref="MaxLocales"/>,
    /// then validates only the bounded token list (avoids O(n) parse work on oversized bodies).
    /// </summary>
    private static IRuleBuilderOptionsConditions<T, IReadOnlyCollection<string>> MustBeBoundedCultureTokens<T>(
        this IRuleBuilder<T, IReadOnlyCollection<string>> ruleBuilder,
        bool allowSyntheticDefault,
        string tooManyMessage,
        string invalidTokenMessage) =>
        ruleBuilder.Custom((locales, context) =>
        {
            List<string> tokens = [];
            foreach (var token in TranslationLocaleList.Tokenize(locales))
            {
                if (tokens.Count == MaxLocales)
                {
                    context.AddFailure(tooManyMessage);
                    return;
                }

                tokens.Add(token);
            }

            foreach (var token in tokens)
            {
                if (!CultureCode.TryParse(token, out var culture)
                    || (!allowSyntheticDefault && culture.IsSyntheticDefault))
                {
                    context.AddFailure(invalidTokenMessage);
                    return;
                }
            }
        });
}
