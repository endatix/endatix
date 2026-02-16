namespace Endatix.Infrastructure.Storage;

/// <summary>
/// Azure Blob Storage provider options used for URL detection when rewriting
/// submission file URLs in exports. Path convention: {container}/s/{formId}/{submissionId}/{fileName}.
/// </summary>
public sealed class AzureBlobStorageProviderOptions
{
    /// <summary>
    /// Storage account host name (e.g. account.blob.core.windows.net or custom domain).
    /// </summary>
    public string HostName { get; set; } = string.Empty;

    /// <summary>
    /// Container name used for submission files (path prefix s/), e.g. user-files
    /// </summary>
    public string UserFilesContainerName { get; set; } = "user-files";

    /// <summary>
    /// Whether this provider is configured enough to detect submission-file URLs.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(HostName) && !string.IsNullOrWhiteSpace(UserFilesContainerName);
}
