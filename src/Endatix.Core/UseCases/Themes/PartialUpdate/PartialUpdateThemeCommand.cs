using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Models.Themes;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Themes.PartialUpdate;

/// <summary>
/// Command for partially updating a theme.
/// </summary>
public record PartialUpdateThemeCommand : ICommand<Result<Theme>>
{
    /// <summary>
    /// The ID of the theme to update.
    /// </summary>
    public long ThemeId { get; }

    /// <summary>
    /// The new name for the theme (optional).
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// The new description for the theme (optional).
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// The new theme data (optional).
    /// </summary>
    public ThemeData? ThemeData { get; }

    /// <summary>
    /// Creates a new instance of PartialUpdateThemeCommand.
    /// </summary>
    /// <param name="themeId">The ID of the theme to update.</param>
    /// <param name="name">The new name for the theme (optional).</param>
    /// <param name="description">The new description for the theme (optional).</param>
    /// <param name="themeData">The new theme data (optional).</param>
    public PartialUpdateThemeCommand(long themeId, string? name = null, string? description = null, ThemeData? themeData = null)
    {
        ThemeId = themeId;
        Name = name;
        Description = description;
        ThemeData = themeData;
    }
}