using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

/// <summary>
/// Represents a folder in the Endatix workspace.
/// </summary>
public sealed class Folder : TenantEntity, IAggregateRoot, IHasUrlSlug
{
    /// <summary>
    /// Unique entity constraints that enforce folder identity per tenant. 
    /// Values should be used as domain and database indexes to enforce uniqueness.
    /// </summary>
    public static class UniqueConstraints
    {
        /// <summary>Unique URL slug segment per tenant.</summary>
        public const string UrlSlugPerTenant = "IX_Folders_TenantId_Slug_Unique";

        /// <summary>Unique display name per tenant via normalized name.</summary>
        public const string NormalizedNamePerTenant = "IX_Folders_TenantId_NormalizedName_Unique";
    }

    // EF Core
    private Folder()
    {
    } 

    /// <summary>
    /// Initializes a new instance of the <see cref="Folder"/> class.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="name">The name of the folder.</param>
    /// <param name="slug">The slug of the folder.</param>
    /// <param name="normalizedName">The normalized folder name (e.g. for case-insensitive uniqueness).</param>
    /// <param name="description">The description of the folder.</param>
    /// <param name="parentFolderId">The ID of the parent folder.</param>
    /// <param name="metadata">The metadata of the folder.</param>
    public Folder(
        long tenantId,
        string name,
        string slug,
        string normalizedName,
        string? description = null,
        long? parentFolderId = null,
        string? metadata = null)
        : base(tenantId)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.NullOrWhiteSpace(slug);
        Guard.Against.NullOrWhiteSpace(normalizedName);

        Name = name;
        UrlSlug = slug;
        NormalizedName = normalizedName;
        Description = description;
        ParentFolderId = parentFolderId;
        Metadata = metadata;
        IsActive = true;
        Immutable = false;
    }

    /// <summary>
    /// Gets or sets the name of the folder.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the normalized name used for uniqueness checks (aligned with Identity lookup normalization).
    /// </summary>
    public string NormalizedName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the URL slug of the folder.
    /// </summary>
    public string UrlSlug { get; set; } = null!;

    /// <summary>
    /// Gets or sets the description of the folder.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the active status of the folder.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the immutable status of the folder.
    /// </summary>
    public bool Immutable { get; set; }

    /// <summary>
    /// Gets or sets the metadata of the folder.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the parent folder ID of the folder.
    /// </summary>
    public long? ParentFolderId { get; set; }

    /// <summary>
    /// Gets or sets the parent folder of the folder.
    /// </summary>
    public Folder? ParentFolder { get; set; }

    /// <summary>
    /// Checks if the folder can be modified.
    /// </summary>
    /// <param name="name">The name of the folder.</param>
    /// <param name="slug">The slug of the folder.</param>
    /// <param name="description">The description of the folder.</param>
    /// <param name="metadata">The metadata of the folder.</param>
    /// <returns>True if the folder can be modified, false otherwise.</returns>
    public bool CanModifyMutableState(string? name, string? slug, string? description, string? metadata) =>
        !Immutable || (name is null && slug is null && description is null && metadata is null);
}
