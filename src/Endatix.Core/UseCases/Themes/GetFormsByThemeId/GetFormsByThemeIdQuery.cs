using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Themes.GetFormsByThemeId;

/// <summary>
/// Query for retrieving all forms using a specific theme.
/// </summary>
public record GetFormsByThemeIdQuery : IQuery<Result<List<Form>>>
{
    /// <summary>
    /// The ID of the theme.
    /// </summary>
    public long ThemeId { get; }
    
    /// <summary>
    /// Creates a new instance of GetFormsByThemeIdQuery.
    /// </summary>
    /// <param name="themeId">The ID of the theme.</param>
    public GetFormsByThemeIdQuery(long themeId)
    {
        Guard.Against.NegativeOrZero(themeId, nameof(themeId));
        ThemeId = themeId;
    }
} 