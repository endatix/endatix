namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Contains HTTP headers for file exports.
/// </summary>
public sealed class ExportHeaders
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
    /// Initializes a new instance of the <see cref="ExportHeaders"/> class.
    /// </summary>
    public ExportHeaders(string contentType, string fileName)
    {
        ContentType = contentType;
        FileName = fileName;
    }
} 