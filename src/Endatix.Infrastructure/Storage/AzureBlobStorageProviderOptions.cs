namespace Endatix.Infrastructure.Storage;

/// <summary>
/// Azure Blob Storage provider options used for URL detection when rewriting
/// submission file URLs in exports and validating submission file URL fetches.
/// Path convention: {container}/s/{formId}/{submissionId}/{fileName}.
/// </summary>
public sealed class AzureBlobStorageProviderOptions
{
    /// <summary>
    /// Storage account host name without scheme or port (e.g. account.blob.core.windows.net, localhost).
    /// </summary>
    public string HostName { get; set; } = string.Empty;

    /// <summary>
    /// Optional TCP port for the storage HTTP endpoint. When omitted, uses 443 for https and 80 for http.
    /// Set for local emulators (e.g. Azurite blob on 10000, RustFS on 9000).
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Container name used for submission files (path prefix s/), e.g. user-files
    /// </summary>
    public string UserFilesContainerName { get; set; } = "user-files";

    /// <summary>
    /// Whether this provider is configured enough to detect submission-file URLs.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(HostName) &&
        !string.IsNullOrWhiteSpace(UserFilesContainerName);

    internal string NormalizedHost => HostName.Trim();

    internal string NormalizedContainerName =>
        UserFilesContainerName.Trim().ToLowerInvariant();

    internal int ResolvePort(string scheme) =>
        Port ?? (scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80);

    internal bool MatchesEndpoint(Uri uri) =>
        string.Equals(NormalizedHost, uri.Host, StringComparison.OrdinalIgnoreCase) &&
        uri.Port == ResolvePort(uri.Scheme);
}
