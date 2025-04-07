namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Request model for getting a theme by ID.
/// </summary>
public class GetByIdRequest
{
    /// <summary>
    /// The ID of the theme.
    /// </summary>
    public long ThemeId { get; set; }
}