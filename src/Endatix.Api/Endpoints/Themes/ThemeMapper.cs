using Endatix.Core.Entities;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Mapper from a theme entity to a theme API model.
/// </summary>
public static class ThemeMapper
{
    /// <summary>
    /// Maps a theme entity to a theme API model.
    /// </summary>
    /// <typeparam name="T">The type of the theme API model, which inherits ThemeModel.</typeparam>
    /// <param name="theme">The theme entity.</param>
    /// <returns>The mapped theme API model.</returns>
    public static T Map<T>(Theme theme) where T : ThemeModel, new() => new T
    {
        Id = theme.Id.ToString(),
        Name = theme.Name,
        Description = theme.Description,
        JsonData = theme.JsonData,
        CreatedAt = theme.CreatedAt,
        ModifiedAt = theme.ModifiedAt,
        FormsCount = theme.Forms?.Count ?? 0
    };

    /// <summary>
    /// Maps a collection of theme entities to a collection of theme API models.
    /// </summary>
    /// <typeparam name="T">The type of the theme API model, which inherits ThemeModel.</typeparam>
    /// <param name="themes">The collection of theme entities.</param>
    /// <returns>A collection of mapped theme API models.</returns>
    public static IEnumerable<T> Map<T>(IEnumerable<Theme> themes) where T : ThemeModel, new() =>
        themes.Select(Map<T>).ToList();
}

/// <summary>
/// Extension methods for mapping theme entities to API models.
/// </summary>
public static class ThemeMapperExtensions
{
    /// <summary>
    /// Maps a collection of theme entities to theme API models without JSON data.
    /// </summary>
    /// <param name="themes">The collection of theme entities.</param>
    /// <returns>A collection of mapped theme API models without JSON data.</returns>
    public static IEnumerable<ThemeModelWithoutJsonData> ToThemeModelList(this IEnumerable<Theme> themes)
    {
        return themes.Select(theme => ThemeMapper.Map<ThemeModelWithoutJsonData>(theme));
    }

    /// <summary>
    /// Maps a collection of theme entities to theme API models with full JSON data.
    /// </summary>
    /// <param name="themes">The collection of theme entities.</param>
    /// <returns>A collection of mapped theme API models with full JSON data.</returns>
    public static IEnumerable<ThemeModel> ToFullThemeModelList(this IEnumerable<Theme> themes)
    {
        return themes.Select(theme => ThemeMapper.Map<ThemeModel>(theme));
    }
} 