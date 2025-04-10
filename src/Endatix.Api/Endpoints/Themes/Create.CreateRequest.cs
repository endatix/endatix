using System.ComponentModel.DataAnnotations;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Request model for creating a new theme.
/// </summary>
public class CreateRequest
{
    /// <summary>
    /// The name of the theme.
    /// </summary>
    [Required]
    public string? Name { get; set; }

    /// <summary>
    /// The description of the theme (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The JSON data representing theme properties (optional).
    /// </summary>
    public string? JsonData { get; set; }
}