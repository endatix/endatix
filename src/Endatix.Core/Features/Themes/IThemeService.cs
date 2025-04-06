using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Features.Themes;

/// <summary>
/// Service for managing themes
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Creates a new theme
    /// </summary>
    /// <param name="name">The theme name</param>
    /// <param name="description">The theme description (optional)</param>
    /// <param name="themeData">The theme data (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the created theme</returns>
    Task<Result<Theme>> CreateThemeAsync(
        string name, 
        string? description = null, 
        ThemeData? themeData = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a theme by ID
    /// </summary>
    /// <param name="themeId">The theme ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the theme</returns>
    Task<Result<Theme>> GetThemeByIdAsync(long themeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all themes for the current tenant
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing a list of themes</returns>
    Task<Result<List<Theme>>> GetThemesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a theme
    /// </summary>
    /// <param name="themeId">The theme ID</param>
    /// <param name="name">The new theme name (optional)</param>
    /// <param name="description">The new theme description (optional)</param>
    /// <param name="themeData">The new theme data (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the updated theme</returns>
    Task<Result<Theme>> UpdateThemeAsync(
        long themeId, 
        string? name = null, 
        string? description = null, 
        ThemeData? themeData = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a theme
    /// </summary>
    /// <param name="themeId">The theme ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> DeleteThemeAsync(long themeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all forms using a theme
    /// </summary>
    /// <param name="themeId">The theme ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing a list of forms</returns>
    Task<Result<List<Form>>> GetFormsByThemeIdAsync(long themeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a theme to a form
    /// </summary>
    /// <param name="formId">The form ID</param>
    /// <param name="themeId">The theme ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the updated form</returns>
    Task<Result<Form>> AssignThemeToFormAsync(long formId, long themeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a theme from a form
    /// </summary>
    /// <param name="formId">The form ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the updated form</returns>
    Task<Result<Form>> RemoveThemeFromFormAsync(long formId, CancellationToken cancellationToken = default);
} 