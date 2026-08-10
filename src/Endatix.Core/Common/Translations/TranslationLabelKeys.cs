namespace Endatix.Core.Common.Translations;

/// <summary>
/// Resolves culture codes to the JSON label keys used in SurveyJS translation maps.
/// </summary>
public static class TranslationLabelKeys
{
    /// <summary>
    /// Maps a culture to the stored label key for <paramref name="catalog"/>.
    /// Synthetic <c>default</c> and the catalog's default culture both resolve to
    /// <see cref="SurveyJsTranslationKeys.DefaultKey"/>. Other catalog cultures resolve to themselves.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the culture is allowed; otherwise <see langword="false"/>.
    /// </returns>
    public static bool TryResolveLabelKey(
        this IHasTranslations catalog,
        CultureCode culture,
        out string labelKey)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (catalog.IsDefaultKey(culture))
        {
            labelKey = SurveyJsTranslationKeys.DefaultKey;
            return true;
        }

        if (catalog.AllowsTranslationKey(culture))
        {
            labelKey = culture.Value;
            return true;
        }

        labelKey = null!;
        return false;
    }
}
