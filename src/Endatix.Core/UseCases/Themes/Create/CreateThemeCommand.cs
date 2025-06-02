using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Themes.Create;

/// <summary>
/// Command for creating a new theme.
/// </summary>
public record CreateThemeCommand : ICommand<Result<Theme>>
{
    /// <summary>
    /// The name of the theme.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Optional description for the theme.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Optional theme data containing visual properties in stringified JSON format.
    /// </summary>
    public string? ThemeData { get; }

    /// <summary>
    /// Creates a new instance of CreateThemeCommand.
    /// </summary>
    /// <param name="name">The name of the theme.</param>
    /// <param name="description">Optional description for the theme.</param>
    /// <param name="themeData">Optional theme data containing visual properties.</param>
    public CreateThemeCommand(string name, string? description = null, string? themeData = null)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));

        Name = name;
        Description = description;
        ThemeData = themeData;
    }
}