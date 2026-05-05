using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// Specifications for working with Folder entities
/// </summary>
public static class FolderSpecifications
{
    /// <summary>
    /// Specification to get a folder by ID
    /// </summary>
    public sealed class FolderByIdSpec : SingleResultSpecification<Folder>
    {
        public FolderByIdSpec(long folderId)
        {
            Query.Where(f => f.Id == folderId);
        }
    }

    /// <summary>
    /// Specification to get a folder by slug
    /// </summary>
    public sealed class FolderBySlugSpec : SingleResultSpecification<Folder>
    {
        public FolderBySlugSpec(string slug)
        {
            Query.Where(f => f.UrlSlug == slug && f.IsActive);
        }
    }

    /// <summary>
    /// Specification to get active folders
    /// </summary>
    public sealed class ActiveFoldersSpec : Specification<Folder>
    {
        public ActiveFoldersSpec()
        {
            Query
                .Where(f => f.IsActive)
                .OrderBy(f => f.Name);
        }
    }

    /// <summary>
    /// All folders (active and inactive) visible through global query filters.
    /// </summary>
    public sealed class AllFoldersSpec : Specification<Folder>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AllFoldersSpec"/> class.
        /// </summary>
        public AllFoldersSpec()
        {
            Query
                .OrderBy(f => f.Name);
        }
    }

    /// <summary>
    /// Specification to check if a folder exists by slug
    /// </summary>
    public sealed class FolderExistsBySlugSpec : SingleResultSpecification<Folder>
    {
        public FolderExistsBySlugSpec(string slug, long? excludeFolderId = null)
        {
            Query.Where(f => f.UrlSlug == slug);
            if (excludeFolderId.HasValue)
            {
                Query.Where(f => f.Id != excludeFolderId.Value);
            }
        }
    }

    /// <summary>
    /// Specification to check if a folder exists by name
    /// </summary>
    public sealed class FolderExistsByNameSpec : SingleResultSpecification<Folder>
    {
        public FolderExistsByNameSpec(string name, long? excludeFolderId = null)
        {
            Query.Where(f => f.Name == name);
            if (excludeFolderId.HasValue)
            {
                Query.Where(f => f.Id != excludeFolderId.Value);
            }
        }
    }
}
