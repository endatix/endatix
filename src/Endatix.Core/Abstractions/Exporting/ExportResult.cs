namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Represents the result of an export operation.
/// </summary>
public sealed record ExportResult
{
    /// <summary>
    /// Gets the content type of the exported file.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Gets the filename of the exported file.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Gets whether the export operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the error message if the export operation was not successful.
    /// </summary>
    public string? ErrorMessage { get; }

    private ExportResult(
        string contentType,
        string fileName,
        bool isSuccess,
        string? errorMessage)
    {
        ContentType = contentType;
        FileName = fileName;
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates a successful export result from export headers.
    /// </summary>
    public static ExportResult Success(ExportHeaders headers) =>
        new(headers.ContentType, headers.FileName, true, null);

    /// <summary>
    /// Creates a successful export result from export file result.
    /// </summary>
    public static ExportResult Success(ExportFileResult fileResult) =>
        new(fileResult.ContentType, fileResult.FileName, true, null);

    /// <summary>
    /// Creates a failed export result.
    /// </summary>
    public static ExportResult Failure(string errorMessage) =>
        new("text/plain", "export-error.txt", false, errorMessage);
}