using Endatix.Core.Entities;
using Endatix.Core.UseCases.Folders;

namespace Endatix.Api.Endpoints.Folders;

/// <summary>
/// Mapper for the <see cref="Folder"/> entity.
/// </summary>
public static class FolderMapper
{
    /// <summary>
    /// Map a <see cref="FolderDto"/> to a <see cref="FolderModel"/>.
    /// </summary>
    /// <param name="dto">The <see cref="FolderDto"/> to map.</param>
    /// <returns>The mapped <see cref="FolderModel"/>.</returns>
    public static FolderModel ToModel(this FolderDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Slug = dto.Slug,
        Description = dto.Description,
        Metadata = dto.Metadata,
        IsActive = dto.IsActive,
        Immutable = dto.Immutable,
        CreatedAt = dto.CreatedAt,
        ModifiedAt = dto.ModifiedAt,
    };

    /// <summary>
    /// Map a <see cref="Folder"/> to a <see cref="FolderModel"/>.
    /// </summary>
    /// <param name="folder">The <see cref="Folder"/> to map.</param>
    /// <returns>The mapped <see cref="FolderModel"/>.</returns>
    public static FolderModel ToModel(this Folder folder) => new()
    {
        Id = folder.Id,
        Name = folder.Name,
        Slug = folder.UrlSlug,
        Description = folder.Description,
        Metadata = folder.Metadata,
        IsActive = folder.IsActive,
        Immutable = folder.Immutable,
        CreatedAt = folder.CreatedAt,
        ModifiedAt = folder.ModifiedAt,
    };

    /// <summary>
    /// Map a collection of <see cref="FolderDto"/> to a collection of <see cref="FolderModel"/>.
    /// </summary>
    /// <param name="folders">The collection of <see cref="FolderDto"/> to map.</param>
    /// <returns>The mapped collection of <see cref="FolderModel"/>.</returns>
    public static IEnumerable<FolderModel> ToModelList(this IEnumerable<FolderDto> folders) =>
        folders.Select(f => f.ToModel());
}
