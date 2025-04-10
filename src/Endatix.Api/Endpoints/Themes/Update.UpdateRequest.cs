using System.ComponentModel.DataAnnotations;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Request model for updating an existing theme.
/// </summary>
public class UpdateRequest
{
    /// <summary>
    /// The ID of the theme to update.
    /// </summary>
    public long ThemeId { get; set; }
    
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