using System.Globalization;
using Endatix.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.Submissions;

/// <summary>
/// Validates submission file storage URLs against canonical path and configured Azure Blob host.
/// </summary>
public sealed class SubmissionFileUrlPolicy
{
    private readonly AzureBlobStorageProviderOptions _azure;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubmissionFileUrlPolicy"/> class.
    /// </summary>
    /// <param name="azureOptions">The Azure Blob Storage provider options.</param>
    public SubmissionFileUrlPolicy(IOptions<AzureBlobStorageProviderOptions> azureOptions)
    {
        _azure = azureOptions.Value;
    }

    /// <summary>
    /// Tries to parse the canonical path of a submission file URL to ensure it matches the expected format and is legitimate.
    /// </summary>
    /// <param name="uri">The URI to parse.</param>
    /// <param name="formId">The form ID.</param>
    /// <param name="submissionId">The submission ID.</param>
    /// <param name="fileName">The file name.</param>
    /// <returns>True if the canonical path is valid, false otherwise.</returns>
    public bool TryParseCanonicalPath(Uri uri, long formId, long submissionId, out string fileName)
    {
        fileName = string.Empty;

        if (!TryValidateSubmissionFileUri(uri, out _))
        {
            return false;
        }

        if (!_azure.IsConfigured || !IsAllowedHost(uri.Host))
        {
            return false;
        }

        return TryMatchCanonicalPath(uri, formId, submissionId, _azure.NormalizedContainerName, out fileName);
    }

    public bool TryValidateForFetch(string url, long formId, long submissionId)
    {
        if (!_azure.IsConfigured)
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !TryValidateSubmissionFileUri(uri, out _))
        {
            return false;
        }

        if (!IsAllowedHost(uri.Host))
        {
            return false;
        }

        return TryMatchCanonicalPath(uri, formId, submissionId, _azure.NormalizedContainerName, out _);
    }

    internal static bool TryMatchCanonicalPath(
        Uri uri,
        long formId,
        long submissionId,
        string containerName,
        out string fileName)
    {
        fileName = string.Empty;
        var segments = uri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 5)
        {
            return false;
        }

        if (segments.Any(static segment => segment is "." or ".."))
        {
            return false;
        }

        if (!string.Equals(segments[0], containerName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(segments[1], "s", StringComparison.Ordinal))
        {
            return false;
        }

        if (!long.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out var urlFormId) ||
            urlFormId != formId)
        {
            return false;
        }

        if (!long.TryParse(segments[3], NumberStyles.None, CultureInfo.InvariantCulture, out var urlSubmissionId) ||
            urlSubmissionId != submissionId)
        {
            return false;
        }

        fileName = string.Join('/', segments[4..]);
        return !string.IsNullOrWhiteSpace(fileName);
    }

    private bool IsAllowedHost(string host) =>
        string.Equals(_azure.HostName.Trim(), host, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Validates that <paramref name="uri"/> is an absolute http(s) storage file URL without embedded credentials.
    /// </summary>
    /// <param name="uri">The URI to validate.</param>
    /// <param name="scheme">The validated scheme (<c>http</c> or <c>https</c>) when validation succeeds.</param>
    /// <returns><see langword="true"/> when the URI is acceptable for submission file fetch validation.</returns>
    private static bool TryValidateSubmissionFileUri(Uri uri, out string scheme)
    {
        scheme = uri.Scheme;
        if (scheme is not ("http" or "https"))
        {
            scheme = string.Empty;
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            scheme = string.Empty;
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            scheme = string.Empty;
            return false;
        }

        return true;
    }
}
