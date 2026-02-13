namespace Endatix.Infrastructure.Storage;

/// <summary>
/// Provider-based storage configuration to support multiple providers
/// </summary>
public sealed class StorageOptions
{
    /// <summary>
    /// The configuration section name where these options are stored.
    /// </summary>
    public const string SectionName = "Endatix:Storage";

    /// <summary>
    /// Dynamic provider configurations. Each key is a provider name (e.g. "AzureBlob");
    /// values are provider-specific config bound to <see cref="SectionName"/>:Providers:{key}.
    /// </summary>
    public Dictionary<string, object> Providers { get; set; } = new();
}
