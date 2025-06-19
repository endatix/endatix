using System.Text.Json;

namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// Defines a contract for extracting files from a submission's JSON data.
/// </summary>
public interface ISubmissionFileExtractor
{
    /// <summary>
    /// Represents a file extracted from a submission.
    /// </summary>
    /// <param name="FileName">The sanitized file name.</param>
    /// <param name="MimeType">The MIME type of the file.</param>
    /// <param name="Content">The file content as a stream.</param>
    record ExtractedFile(string FileName, string MimeType, Stream Content);

    /// <summary>
    /// Extracts files from a submission's JSON data by recursively traversing the JSON structure.
    /// </summary>
    /// <param name="root">The root JSON element to search for file objects.</param>
    /// <param name="submissionId">The ID of the submission being processed.</param>
    /// <param name="prefix">Optional prefix to prepend to extracted file names.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>A list of extracted files with their metadata and content streams.</returns>
    Task<List<ExtractedFile>> ExtractFilesAsync(JsonElement root, long submissionId, string prefix = "", CancellationToken cancellationToken = default);
}