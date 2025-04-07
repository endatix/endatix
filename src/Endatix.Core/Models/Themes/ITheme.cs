using System.Text.Json.Serialization;

namespace Endatix.Core.Models.Themes;

/// <summary>
/// Represents the structure of theme data stored in the Theme entity's JsonData field.
/// This mirrors the structure of the survey-core ITheme TypeScript interface.
/// </summary>
public interface ITheme
{
    /// <summary>
    /// Name of the theme
    /// </summary>
    [JsonPropertyName("themeName")]
    string ThemeName { get; set; }
    
    /// <summary>
    /// Color palette, either "light" or "dark"
    /// </summary>
    [JsonPropertyName("colorPalette")]
    string ColorPalette { get; set; }
    
    /// <summary>
    /// Whether the theme is panelless
    /// </summary>
    [JsonPropertyName("isPanelless")]
    bool IsPanelless { get; set; }
    
    /// <summary>
    /// Background image URL
    /// </summary>
    [JsonPropertyName("backgroundImage")]
    string? BackgroundImage { get; set; }
    
    /// <summary>
    /// Background image fit style
    /// </summary>
    [JsonPropertyName("backgroundImageFit")]
    string? BackgroundImageFit { get; set; }
    
    /// <summary>
    /// Background image attachment
    /// </summary>
    [JsonPropertyName("backgroundImageAttachment")]
    string? BackgroundImageAttachment { get; set; }
    
    /// <summary>
    /// Background opacity (0-1)
    /// </summary>
    [JsonPropertyName("backgroundOpacity")]
    double? BackgroundOpacity { get; set; }
    
    /// <summary>
    /// CSS variables for theming
    /// </summary>
    [JsonPropertyName("cssVariables")]
    Dictionary<string, string>? CssVariables { get; set; }
    
    /// <summary>
    /// Header configuration
    /// </summary>
    [JsonPropertyName("header")]
    Dictionary<string, object>? Header { get; set; }
    
    /// <summary>
    /// Header view type
    /// </summary>
    [JsonPropertyName("headerView")]
    string? HeaderView { get; set; }
    
    /// <summary>
    /// Additional theme properties
    /// </summary>
    [JsonPropertyName("additionalProperties")]
    Dictionary<string, object>? AdditionalProperties { get; set; }
} 