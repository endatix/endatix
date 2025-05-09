using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Models.Themes;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Themes.Update;

/// <summary>
/// Command for updating an existing theme.
/// </summary>
public record UpdateThemeCommand : ICommand<Result<Theme>>
{
    /// <summary>
    /// The ID of the theme to update.
    /// </summary>
    public long ThemeId { get; }

    /// <summary>
    /// The new name for the theme.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The new description for the theme.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// The new theme data containing visual properties as a JSON string.
    /// </summary>
    public string? ThemeData { get; }

    /// <summary>
    /// Creates a new instance of UpdateThemeCommand.
    /// </summary>
    /// <param name="themeId">The ID of the theme to update.</param>
    /// <param name="name">The new name for the theme.</param>
    /// <param name="description">The new description for the theme.</param>
    /// <param name="themeData">The new theme data containing visual properties as a JSON string.</param>
    public UpdateThemeCommand(long themeId, string name, string? description = null, string? themeData = null)
    {
        ThemeId = themeId;
        Name = name;
        Description = description;
        ThemeData = themeData;
    }
}