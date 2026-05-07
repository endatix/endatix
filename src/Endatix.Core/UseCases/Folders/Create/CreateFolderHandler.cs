using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Common;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.Folders.Create;

/// <summary>
/// Handler for creating a folder.
/// </summary>
public sealed class CreateFolderHandler(
    IRepository<Folder> folderRepository,
    IMediator mediator,
    ITenantContext tenantContext,
    FolderWritePolicy folderWritePolicy,
    IUniqueConstraintViolationChecker uniqueConstraintViolationChecker)
    : ICommandHandler<CreateFolderCommand, Result<Folder>>
{
    /// <inheritdoc/>
    public async Task<Result<Folder>> Handle(CreateFolderCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);

        var nameResult = folderWritePolicy.NormalizeNameOrError(request.Name);
        if (!nameResult.IsSuccess)
        {
            return Result.Invalid(folderWritePolicy.CreateNameNormalizationValidationError(nameof(CreateFolderCommand.Name)));
        }

        var (trimmedName, normalizedName) = nameResult.Value;
        var existingByNormalizedName = await FolderWritePolicy.NormalizedNameExistsAsync(
            folderRepository,
            normalizedName,
            excludeFolderId: null,
            cancellationToken);
        if (existingByNormalizedName)
        {
            return Result.Invalid(folderWritePolicy.CreateDuplicateNameValidationError(trimmedName, nameof(CreateFolderCommand.Name)));
        }

        var slugResult = folderWritePolicy.NormalizeAndValidateSlugOrError(request.Slug, trimmedName, includeDetailedInvalidMessage: true);
        if (!slugResult.IsSuccess || slugResult.Value is null)
        {
            return Result.Invalid(folderWritePolicy.CreateInvalidSlugValidationError(
                slugResult.Errors.FirstOrDefault() ?? "Slug is invalid.",
                nameof(CreateFolderCommand.Slug)));
        }

        var baseSlug = slugResult.Value;
        var slugTaken = await folderWritePolicy.SlugExistsAsync(baseSlug, excludeFolderId: null, cancellationToken);
        if (slugTaken)
        {
            return Result.Invalid(folderWritePolicy.CreateDuplicateSlugValidationError(baseSlug, nameof(CreateFolderCommand.Slug)));
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

            if (violation.IsFolderSlugViolation())
            {
                return Result.Invalid(folderWritePolicy.CreateDuplicateSlugValidationError(baseSlug, nameof(CreateFolderCommand.Slug)));
            }

            if (violation.IsFolderNameViolation())
            {
                return Result.Invalid(folderWritePolicy.CreateDuplicateNameValidationError(trimmedName, nameof(CreateFolderCommand.Name)));
            }

            return Result.Invalid(folderWritePolicy.CreateDuplicateFolderConstraintValidationError(
                trimmedName,
                baseSlug,
                nameof(CreateFolderCommand.Name)));
        }

        await mediator.Publish(new FolderCreatedEvent(folder), cancellationToken);
        return Result<Folder>.Created(folder);
    }

}
