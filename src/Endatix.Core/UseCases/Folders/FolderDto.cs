using Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Folders;

/// <summary>
/// DTO for a folder.
/// </summary>
public sealed record FolderDto
{
    internal static FolderDto From(Folder f) => new()
    {
        Id = f.Id,
        Name = f.Name,
        Slug = f.UrlSlug,
        Description = f.Description,
        Metadata = f.Metadata,
        IsActive = f.IsActive,
        Immutable = f.Immutable,
        CreatedAt = f.CreatedAt,
        ModifiedAt = f.ModifiedAt
    };

    /// <summary>
    /// The ID of the folder.
    /// </summary>
    public required long Id { get; init; }
    /// <summary>
    /// The name of the folder.
    /// </summary>
    public required string Name { get; init; } = string.Empty;

    /// <summary>
    /// The slug of the folder.
    /// </summary>
    public required string Slug { get; init; } = string.Empty;
    /// <summary>
    /// The description of the folder.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The metadata of the folder.
    /// </summary>
    public string? Metadata { get; init; }
    /// <summary>
    /// Whether the folder is active.
    /// </summary>
    public bool IsActive { get; init; }
    /// <summary>
    /// Whether the folder is immutable.
    /// </summary>
    public bool Immutable { get; init; }

    /// <summary>
    /// The date and time when the folder was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }
    /// <summary>
    /// The date and time when the folder was last modified.
    /// </summary>
    public DateTime? ModifiedAt { get; init; }
}
