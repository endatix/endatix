using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Common;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Folders.Create;

namespace Endatix.Core.UseCases.Folders;

/// <summary>
/// Shared write rules for folder create/update flows.
/// </summary>
public sealed class FolderWritePolicy(
    IRepository<Folder> folderRepository,
    IValueNormalizer valueNormalizer,
    ITenantContext? scope)
{
    /// <summary>Constructs the ordinary folder write policy without an explicit tenant context.</summary>
    public FolderWritePolicy(IRepository<Folder> folderRepository, IValueNormalizer valueNormalizer)
        : this(folderRepository, valueNormalizer, null) { }

    internal ITenantContext? Scope => scope;

    internal Task<bool> NameExistsAsync(string normalizedName, long? excludeFolderId, CancellationToken ct)
    {
        var spec = NamedFolder(normalizedName, excludeFolderId, scope);
        return folderRepository.AnyAsync(spec, ct);
    }

    public ValidationError CreateNameNormalizationValidationError(string identifier) => new()
    {
        Identifier = identifier,
        ErrorMessage = "Folder name could not be normalized."
    };

    internal Result<Folder>? UniqueViolation(Folder folder, UniqueConstraintViolationResult violation)
    {
        if (!violation.IsUniqueConstraintViolation)
        {
            return null;
        }
        if (violation.IsFolderSlugViolation())
        {
            return Result.Invalid(CreateDuplicateSlugValidationError(folder.UrlSlug, nameof(CreateFolderCommand.Slug)));
        }
        var error = violation.IsFolderNameViolation() ? CreateDuplicateNameValidationError(folder.Name, nameof(CreateFolderCommand.Name))
            : CreateDuplicateFolderConstraintValidationError(folder.Name, folder.UrlSlug, nameof(CreateFolderCommand.Name));
        return Result.Invalid(error);
    }

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

    /// <summary>Checks the normalized name in the caller's scoped repository.</summary>
    public static Task<bool> NormalizedNameExistsAsync(IRepository<Folder> folderRepository, string normalizedName, long? excludeFolderId, CancellationToken cancellationToken) =>
        folderRepository.AnyAsync(NamedFolder(normalizedName, excludeFolderId, null), cancellationToken);

    private static FolderSpecifications.FolderExistsByNormalizedNameSpec NamedFolder(string normalizedName, long? excludeFolderId, ITenantContext? tenant)
    {
        var spec = new FolderSpecifications.FolderExistsByNormalizedNameSpec(normalizedName, excludeFolderId);
        spec.InTenant(tenant);
        return spec;
    }

    public static Result<string> NormalizeAndValidateSlugOrError(string? rawSlug, string fallbackName, bool includeDetailedInvalidMessage)
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
        spec.InTenant(scope);
        return await folderRepository.AnyAsync(spec, cancellationToken);
    }
}
