using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Themes.GetById;

/// <summary>
/// Query for retrieving a theme by ID.
/// </summary>
public record GetThemeByIdQuery : IQuery<Result<Theme>>
{
    /// <summary>
    /// The ID of the theme to retrieve.
    /// </summary>
    public long ThemeId { get; }

    /// <summary>
    /// Creates a new instance of GetThemeByIdQuery.
    /// </summary>
    /// <param name="themeId">The ID of the theme to retrieve.</param>
    public GetThemeByIdQuery(long themeId)
    {
        ThemeId = themeId;
    }
}