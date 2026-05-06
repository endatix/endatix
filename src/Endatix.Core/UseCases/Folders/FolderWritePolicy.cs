using Endatix.Core.Abstractions;
using Endatix.Core.Common;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Folders;

/// <summary>
/// Shared write rules for folder create/update flows.
/// </summary>
public sealed class FolderWritePolicy(
    IRepository<Folder> folderRepository,
    IValueNormalizer valueNormalizer)
{
    public ValidationError CreateNameNormalizationValidationError(string identifier) => new()
    {
        Identifier = identifier,
        ErrorMessage = "Folder name could not be normalized."
    };

    public ValidationError CreateDuplicateNameValidationError(string name, string identifier) => new()
    {
        Identifier = identifier,
        ErrorMessage = $"A folder with the name '{name}' already exists."
    };

    public ValidationError CreateInvalidSlugValidationError(string message, string identifier) => new()
    {
        Identifier = identifier,
        ErrorMessage = message
    };

    public ValidationError CreateDuplicateSlugValidationError(string slug, string identifier) => new()
    {
        Identifier = identifier,
        ErrorMessage = $"A folder with the slug '{slug}' already exists."
    };

    public ValidationError CreateDuplicateFolderConstraintValidationError(string name, string slug, string identifier) => new()
    {
        Identifier = identifier,
        ErrorMessage = $"A folder with the same name ('{name}') or slug ('{slug}') already exists."
    };

    public ValidationError CreateImmutableFolderValidationError(string identifier) => new()
    {
        Identifier = identifier,
        ErrorMessage = "This folder cannot be modified."
    };

    public Result<(string Name, string NormalizedName)> NormalizeNameOrError(string rawName)
    {
        var trimmedName = rawName.Trim();
        var normalizedName = valueNormalizer.Normalize(trimmedName);
        if (string.IsNullOrEmpty(normalizedName))
        {
            return Result.Error("Folder name could not be normalized.");
        }

        return Result.Success((trimmedName, normalizedName));
    }

    public async Task<bool> NormalizedNameExistsAsync(string normalizedName, long? excludeFolderId, CancellationToken cancellationToken)
    {
        var spec = new FolderSpecifications.FolderExistsByNormalizedNameSpec(normalizedName, excludeFolderId);
        return await folderRepository.AnyAsync(spec, cancellationToken);
    }

    public Result<string> NormalizeAndValidateSlugOrError(string? rawSlug, string fallbackName, bool includeDetailedInvalidMessage)
    {
        var normalizedSlug = string.IsNullOrWhiteSpace(rawSlug)
            ? UrlSlugNormalizer.FromDisplayName(fallbackName)
            : UrlSlugNormalizer.Normalize(rawSlug.Trim());

        if (string.IsNullOrEmpty(normalizedSlug))
        {
            return Result.Error("Slug cannot be empty.");
        }

        if (!UrlSlugNormalizer.IsValidFormat(normalizedSlug))
        {
            return includeDetailedInvalidMessage
                ? Result.Error("Slug format is invalid. Use lowercase letters, numbers, and hyphens only.")
                : Result.Error("Slug format is invalid.");
        }

        if (UrlSlugNormalizer.IsReserved(normalizedSlug))
        {
            return Result.Error("This slug is reserved.");
        }

        return Result.Success(normalizedSlug);
    }

    public async Task<bool> SlugExistsAsync(string slug, long? excludeFolderId, CancellationToken cancellationToken)
    {
        var spec = new FolderSpecifications.FolderExistsBySlugSpec(slug, excludeFolderId);
        return await folderRepository.AnyAsync(spec, cancellationToken);
    }
}
