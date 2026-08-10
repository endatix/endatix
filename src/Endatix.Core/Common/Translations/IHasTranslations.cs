namespace Endatix.Core.Common.Translations;

/// <summary>
/// Aggregate that owns a SurveyJS-style translation catalog (synthetic default key + added cultures).
/// </summary>
public interface IHasTranslations
{
    /// <summary>
    /// Shared domain limit for added cultures on any translations-capable aggregate
    /// (excluding the synthetic <c>default</c> translation key).
    /// </summary>
    const int DEFAULT_MAX_AVAILABLE_CULTURES = 20;

    /// <summary>
    /// Maximum length for a culture code.
    /// </summary>
    const int MAX_CULTURE_CODE_LENGTH = 16;

    /// <summary>
    /// Real culture represented by the SurveyJS <c>default</c> translation key.
    /// </summary>
    string DefaultCulture { get; }

    /// <summary>
    /// Added cultures for this aggregate. Does not include the synthetic <c>default</c> key.
    /// </summary>
    IReadOnlyList<string> AvailableCultures { get; }

    /// <summary>
    /// Maximum number of added cultures (excluding the synthetic default key).
    /// Defaults to <see cref="DEFAULT_MAX_AVAILABLE_CULTURES"/> unless an aggregate overrides it.
    /// </summary>
    int MaxAvailableCultures { get; }

    /// <summary>
    /// Sets which real culture the SurveyJS <c>default</c> key represents.
    /// </summary>
    void SetDefaultCulture(CultureCode cultureCode);

    /// <summary>
    /// Adds a culture to the catalog.
    /// </summary>
    void AddCulture(CultureCode cultureCode);

    /// <summary>
    /// Removes a culture from the catalog and strips related translations from owned content.
    /// </summary>
    void RemoveCulture(CultureCode cultureCode);

    /// <summary>
    /// Returns whether a translation map key is allowed (catalog ∪ synthetic default key).
    /// </summary>
    bool AllowsTranslationKey(CultureCode key);

    /// <summary>
    /// Returns whether <paramref name="cultureCode"/> maps to the synthetic <c>default</c> translation key —
    /// either the literal <c>default</c> key or this aggregate's <see cref="DefaultCulture"/>.
    /// </summary>
    bool IsDefaultKey(CultureCode cultureCode);
}
