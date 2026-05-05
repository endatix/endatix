namespace Endatix.Api.Endpoints.Folders;

/// <summary>
/// Model for a folder.
/// </summary>      
public sealed class FolderModel
{
    /// <summary>
    /// The ID of the folder.
    /// </summary>
    public long Id { get; set; }
    /// <summary>
    /// The name of the folder.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// The slug of the folder.
    /// </summary>
    public string Slug { get; set; } = string.Empty;
    /// <summary>
    /// The description of the folder.
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// The metadata of the folder.
    /// </summary>
    public string? Metadata { get; set; }
    /// <summary>
    /// Indicates if the folder is active.
    /// </summary>
    public bool IsActive { get; set; }
    /// <summary>
    /// Indicates if the folder is immutable.
    /// </summary>
    public bool Immutable { get; set; }

    /// <summary>
    /// The date and time when the folder was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>
    /// The date and time when the folder was last modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }
}
