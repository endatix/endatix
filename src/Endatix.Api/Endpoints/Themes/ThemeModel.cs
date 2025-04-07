using System.Text.Json.Serialization;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// API model representing a theme.
/// </summary>
public class ThemeModel
{
    /// <summary>
    /// The ID of the theme.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The name of the theme.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The description of the theme.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The JSON data representing the theme properties.
    /// </summary>
    public virtual string JsonData { get; set; } = "{}";
    
    /// <summary>
    /// The date and time when the theme was created.
    /// </summary>
    public DateTime? CreatedAt { get; set; }
    
    /// <summary>
    /// The date and time when the theme was last modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }
    
    /// <summary>
    /// The count of forms using this theme.
    /// </summary>
    public int FormsCount { get; set; }
}

/// <summary>
/// API model representing a theme without the full JSON data (for use in lists).
/// </summary>
public class ThemeModelWithoutJsonData : ThemeModel
{
    /// <summary>
    /// Hides the JsonData property from the base model.
    /// </summary>
    [JsonIgnore]
    public override string JsonData { get; set; } = "{}";
} 