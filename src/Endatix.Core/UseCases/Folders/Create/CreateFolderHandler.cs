using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Common;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.Folders.Create;

/// <summary>
/// Handler for creating a folder.
/// </summary>
public sealed class CreateFolderHandler(
    IRepository<Folder> folderRepository,
    IMediator mediator,
    ITenantContext tenantContext,
    IValueNormalizer valueNormalizer,
    IUniqueConstraintViolationChecker uniqueConstraintViolationChecker)
    : ICommandHandler<CreateFolderCommand, Result<Folder>>
{
    /// <inheritdoc/>
    public async Task<Result<Folder>> Handle(CreateFolderCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);

        var trimmedName = request.Name.Trim();
        var normalizedName = valueNormalizer.Normalize(trimmedName);
        if (string.IsNullOrEmpty(normalizedName))
        {
            return Result.Error("Folder name could not be normalized.");
        }

        var normalizedNameExistsSpec = new FolderSpecifications.FolderExistsByNormalizedNameSpec(normalizedName);
        var existingByNormalizedName = await folderRepository.AnyAsync(normalizedNameExistsSpec, cancellationToken);
        if (existingByNormalizedName)
        {
            return Result.Error("A folder with this name already exists.");
        }

        var baseSlug = string.IsNullOrWhiteSpace(request.Slug)
            ? UrlSlugNormalizer.FromDisplayName(trimmedName)
            : UrlSlugNormalizer.Normalize(request.Slug.Trim());

        if (string.IsNullOrEmpty(baseSlug))
        {
            return Result.Error("Slug cannot be empty.");
        }

        if (!UrlSlugNormalizer.IsValidFormat(baseSlug))
        {
            return Result.Error("Slug format is invalid. Use lowercase letters, numbers, and hyphens only.");
        }

        if (UrlSlugNormalizer.IsReserved(baseSlug))
        {
            return Result.Error("This slug is reserved.");
        }

        var existingBySlug = await folderRepository.SingleOrDefaultAsync(
            new FolderSpecifications.FolderExistsBySlugSpec(baseSlug),
            cancellationToken);
        if (existingBySlug is not null)
        {
            return Result.Invalid(CreateDuplicateSlugValidationError(baseSlug));
        }

        var folder = new Folder(
            tenantContext.TenantId,
            trimmedName,
            baseSlug,
            normalizedName,
            request.Description?.Trim(),
            null,
            request.Metadata);

        if (request.Immutable)
        {
            folder.Immutable = true;
        }

        try
        {
            await folderRepository.AddAsync(folder, cancellationToken);
        }
        catch (Exception exception)
        {
            var violation = uniqueConstraintViolationChecker.AnalyzeUniqueConstraint(exception);
            if (!violation.IsUniqueConstraintViolation)
            {
                throw;
            }

            if (IsFolderSlugUniqueViolation(violation))
            {
                return Result.Invalid(CreateDuplicateSlugValidationError(baseSlug));
            }

            if (IsFolderNormalizedNameUniqueViolation(violation))
            {
                return Result.Invalid(CreateDuplicateNameValidationError(trimmedName));
            }

            return Result.Invalid(CreateDuplicateFolderConstraintValidationError(trimmedName, baseSlug));
        }

        await mediator.Publish(new FolderCreatedEvent(folder), cancellationToken);
        return Result<Folder>.Created(folder);
    }

    private static bool IsFolderSlugUniqueViolation(UniqueConstraintViolationResult violation) =>
        string.Equals(violation.ConstraintName, Folder.UniqueConstraints.UrlSlugPerTenant, StringComparison.OrdinalIgnoreCase)
        || string.Equals(violation.ColumnName, nameof(Folder.UrlSlug), StringComparison.OrdinalIgnoreCase);

    private static bool IsFolderNormalizedNameUniqueViolation(UniqueConstraintViolationResult violation) =>
        string.Equals(violation.ConstraintName, Folder.UniqueConstraints.NormalizedNamePerTenant, StringComparison.OrdinalIgnoreCase)
        || string.Equals(violation.ColumnName, nameof(Folder.NormalizedName), StringComparison.OrdinalIgnoreCase);

    private static ValidationError CreateDuplicateNameValidationError(string name) => new()
    {
        Identifier = nameof(CreateFolderCommand.Name),
        ErrorMessage = $"A folder with the name '{name}' already exists."
    };

    private static ValidationError CreateDuplicateSlugValidationError(string slug) => new()
    {
        Identifier = nameof(CreateFolderCommand.Slug),
        ErrorMessage = $"A folder with the slug '{slug}' already exists."
    };

    private static ValidationError CreateDuplicateFolderConstraintValidationError(string name, string slug) => new()
    {
        Identifier = nameof(CreateFolderCommand.Name),
        ErrorMessage = $"A folder with the same name ('{name}') or slug ('{slug}') already exists."
    };
}
