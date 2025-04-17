using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Abstractions.Repositories;

/// <summary>
/// Defines the contract for a repository that handles themes, extending Ardalis.ISpecification
/// </summary>
public interface IThemesRepository : IRepository<Theme>
{
    /// <summary>
    /// Gets all forms using a specific theme
    /// </summary>
    /// <param name="themeId">The theme ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of forms using the theme</returns>
    Task<IReadOnlyList<Form>> GetFormsByThemeIdAsync(long themeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a theme to a form
    /// </summary>
    /// <param name="formId">The form ID</param>
    /// <param name="themeId">The theme ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated form</returns>
    Task<Form> AssignThemeToFormAsync(long formId, long themeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a theme from a form
    /// </summary>
    /// <param name="formId">The form ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated form</returns>
    Task<Form> RemoveThemeFromFormAsync(long formId, CancellationToken cancellationToken = default);
} 