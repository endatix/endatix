namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Request model for getting all forms using a specific theme.
/// </summary>
public class GetFormsByThemeIdRequest
{
    /// <summary>
    /// The ID of the theme.
    /// </summary>
    public long ThemeId { get; set; }
} 