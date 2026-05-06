using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Folders;

/// <summary>
/// Extensions for the <see cref="UniqueConstraintViolationResult"/> class.
/// </summary>
public static class FolderUniqueViolationExtensions
{
    /// <summary>
    /// Checks if the violation is a folder slug violation.
    /// </summary>
    /// <param name="violation">The violation to check.</param>
    /// <returns>True if the violation is a folder slug violation, false otherwise.</returns>
    public static bool IsFolderSlugViolation(this UniqueConstraintViolationResult violation) =>
        string.Equals(violation.ConstraintName, Folder.UniqueConstraints.UrlSlugPerTenant, StringComparison.OrdinalIgnoreCase)
        || string.Equals(violation.ColumnName, nameof(Folder.UrlSlug), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if the violation is a folder name violation.
    /// </summary>
    /// <param name="violation">The violation to check.</param>
    /// <returns>True if the violation is a folder name violation, false otherwise.</returns>
    public static bool IsFolderNameViolation(this UniqueConstraintViolationResult violation) =>
        string.Equals(violation.ConstraintName, Folder.UniqueConstraints.NormalizedNamePerTenant, StringComparison.OrdinalIgnoreCase)
        || string.Equals(violation.ColumnName, nameof(Folder.NormalizedName), StringComparison.OrdinalIgnoreCase);
}
