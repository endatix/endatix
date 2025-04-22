using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Themes.Delete;

/// <summary>
/// Command for deleting a theme.
/// </summary>
public record DeleteThemeCommand : ICommand<Result<string>>
{
    /// <summary>
    /// The ID of the theme to delete.
    /// </summary>
    public long ThemeId { get; }

    /// <summary>
    /// Creates a new instance of DeleteThemeCommand.
    /// </summary>
    /// <param name="themeId">The ID of the theme to delete.</param>
    public DeleteThemeCommand(long themeId)
    {
        ThemeId = themeId;
    }
}