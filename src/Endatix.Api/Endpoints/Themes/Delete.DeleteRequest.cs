namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Request model for deleting a theme.
/// </summary>
public class DeleteRequest
{
    /// <summary>
    /// The ID of the theme to delete.
    /// </summary>
    public long ThemeId { get; set; }
}