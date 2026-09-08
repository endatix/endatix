namespace Endatix.Hosting.DevTools;

/// <summary>
/// Development playground that hosts Hub <c>embed.js</c> on the API origin (cross-origin vs Hub).
/// Bound from <c>Endatix:DevTools:EmbedHost</c>.
/// </summary>
internal sealed class EmbedHostOptions
{
    public const string SectionName = "Endatix:DevTools:EmbedHost";

    /// <summary>
    /// When null, enabled only in Development. Explicit true/false overrides the environment.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    /// Hub origin used for <c>embed.js</c>. Falls back to <c>Endatix:Hub:HubBaseUrl</c>.
    /// </summary>
    public string HubBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Extra hosts allowed in the <c>hubBaseUrl</c> query override (plus localhost and the configured Hub host).
    /// </summary>
    public string[] AllowedHubHosts { get; set; } = [];

    public bool IsEnabled => Enabled == true;
}
