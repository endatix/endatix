namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Represents metadata about an exported file.
/// </summary>
public sealed record FileExport
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
    /// Initializes a new instance of the <see cref="FileExport"/> class.
    /// </summary>
    /// <param name="contentType">The content type of the exported file.</param>
    /// <param name="fileName">The filename of the exported file.</param>
    public FileExport(string contentType, string fileName)
    {
        ContentType = contentType;
        FileName = fileName;
    }
    
    /// <summary>
    /// Creates a FileExport from the specified headers.
    /// </summary>
    public static FileExport FromHeaders(ExportHeaders headers) => 
        new(headers.ContentType, headers.FileName);
} 