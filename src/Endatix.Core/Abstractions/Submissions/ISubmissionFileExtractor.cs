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

    Task<List<ExtractedFile>> ExtractFilesAsync(JsonElement root, string prefix = "", CancellationToken cancellationToken = default);
}