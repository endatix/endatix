namespace Endatix.Core.Common.Translations;

/// <summary>
/// SurveyJS translation key constants shared by multilingual aggregates.
/// </summary>
public static class SurveyJsTranslationKeys
{
    /// <summary>
    /// Synthetic fallback key stored in translated label maps (<c>{"default": "..."}</c>).
    /// </summary>
    public const string DefaultKey = "default";

    /// <summary>
    /// Real culture used when an aggregate does not specify which culture <see cref="DefaultKey"/> represents.
    /// </summary>
    public const string FallbackDefaultCulture = "en";
}
