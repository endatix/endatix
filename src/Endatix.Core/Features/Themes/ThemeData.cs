using System.Text.Json.Serialization;

namespace Endatix.Core.Features.Themes;

/// <summary>
/// Default implementation of ITheme interface
/// </summary>
public class ThemeData : ITheme
{
    /// <summary>
    /// Name of the theme
    /// </summary>
    [JsonPropertyName("themeName")]
    public string ThemeName { get; set; } = "Default Theme";
    
    /// <summary>
    /// Color palette, either "light" or "dark"
    /// </summary>
    [JsonPropertyName("colorPalette")]
    public string ColorPalette { get; set; } = "light";
    
    /// <summary>
    /// Whether the theme is panelless
    /// </summary>
    [JsonPropertyName("isPanelless")]
    public bool IsPanelless { get; set; } = false;
    
    /// <summary>
    /// Background image URL
    /// </summary>
    [JsonPropertyName("backgroundImage")]
    public string? BackgroundImage { get; set; }
    
    /// <summary>
    /// Background image fit style
    /// </summary>
    [JsonPropertyName("backgroundImageFit")]
    public string? BackgroundImageFit { get; set; } = "cover";
    
    /// <summary>
    /// Background image attachment
    /// </summary>
    [JsonPropertyName("backgroundImageAttachment")]
    public string? BackgroundImageAttachment { get; set; } = "scroll";
    
    /// <summary>
    /// Background opacity (0-1)
    /// </summary>
    [JsonPropertyName("backgroundOpacity")]
    public double? BackgroundOpacity { get; set; } = 1.0;
    
    /// <summary>
    /// CSS variables for theming
    /// </summary>
    [JsonPropertyName("cssVariables")]
    public Dictionary<string, string>? CssVariables { get; set; } = new Dictionary<string, string>
    {
        ["--sjs-general-backcolor"] = "rgba(255, 255, 255, 1)",
        ["--sjs-general-forecolor"] = "rgba(34, 34, 34, 1)",
        ["--sjs-primary-backcolor"] = "rgba(0, 123, 255, 1)",
        ["--sjs-primary-forecolor"] = "rgba(255, 255, 255, 1)",
        ["--sjs-font-family"] = "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif"
    };
    
    /// <summary>
    /// Header configuration
    /// </summary>
    [JsonPropertyName("header")]
    public Dictionary<string, object>? Header { get; set; }
    
    /// <summary>
    /// Header view type
    /// </summary>
    [JsonPropertyName("headerView")]
    public string? HeaderView { get; set; }
    
    /// <summary>
    /// Additional theme properties
    /// </summary>
    [JsonPropertyName("additionalProperties")]
    public Dictionary<string, object>? AdditionalProperties { get; set; }
} 