using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.Folders.Update;

/// <summary>
/// Handler for updating a folder.
/// </summary>
public sealed class UpdateFolderHandler(
    IRepository<Folder> folderRepository,
    IMediator mediator,
    FolderWritePolicy folderWritePolicy,
    IUniqueConstraintViolationChecker uniqueConstraintViolationChecker)
    : ICommandHandler<UpdateFolderCommand, Result<Folder>>
{
    /// <inheritdoc/>
    public async Task<Result<Folder>> Handle(UpdateFolderCommand request, CancellationToken cancellationToken)
    {
        var folder = await folderRepository.FirstOrDefaultAsync(
            new FolderSpecifications.FolderByIdSpec(request.FolderId),
            cancellationToken);

        if (folder is null)
        {
            return Result.NotFound("Folder not found.");
        }

        if (request.Immutable is false)
        {
            folder.Immutable = false;
        }

        if (!folder.CanModifyMutableState(request.Name, request.Slug, request.Description, request.Metadata))
        {
            return Result.Invalid(folderWritePolicy.CreateImmutableFolderValidationError(nameof(UpdateFolderCommand.FolderId)));
        }

        if (request.Name is not null)
        {
            var nameResult = folderWritePolicy.NormalizeNameOrError(request.Name);
            if (!nameResult.IsSuccess)
            {
                return Result.Invalid(folderWritePolicy.CreateNameNormalizationValidationError(nameof(UpdateFolderCommand.Name)));
            }
            var (trimmed, normalizedName) = nameResult.Value;

            var nameTaken = await folderWritePolicy.NormalizedNameExistsAsync(normalizedName, folder.Id, cancellationToken);
            if (nameTaken)
            {
                return Result.Invalid(folderWritePolicy.CreateDuplicateNameValidationError(trimmed, nameof(UpdateFolderCommand.Name)));
            }

            folder.Name = trimmed;
            folder.NormalizedName = normalizedName;
        }

        if (request.Slug is not null)
        {
            var slugResult = folderWritePolicy.NormalizeAndValidateSlugOrError(request.Slug, folder.Name, includeDetailedInvalidMessage: false);
            if (!slugResult.IsSuccess || slugResult.Value is null)
            {
                return Result.Invalid(folderWritePolicy.CreateInvalidSlugValidationError(
                    slugResult.Errors.FirstOrDefault() ?? "Slug is invalid.",
                    nameof(UpdateFolderCommand.Slug)));
            }
            var normalized = slugResult.Value;

            var slugTaken = await folderWritePolicy.SlugExistsAsync(normalized, folder.Id, cancellationToken);
            if (slugTaken)
            {
                return Result.Invalid(folderWritePolicy.CreateDuplicateSlugValidationError(normalized, nameof(UpdateFolderCommand.Slug)));
            }

            folder.UrlSlug = normalized;
        }

        if (request.Description is not null)
        {
            folder.Description = request.Description.Trim();
        }

        if (request.Metadata is not null)
        {
            folder.Metadata = request.Metadata;
        }

        if (request.IsActive.HasValue)
        {
            folder.IsActive = request.IsActive.Value;
        }

        if (request.Immutable is true)
        {
            folder.Immutable = true;
        }

        try
        {
            await folderRepository.UpdateAsync(folder, cancellationToken);
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
                return Result.Invalid(folderWritePolicy.CreateDuplicateSlugValidationError(folder.UrlSlug, nameof(UpdateFolderCommand.Slug)));
            }

            if (violation.IsFolderNameViolation())
            {
                return Result.Invalid(folderWritePolicy.CreateDuplicateNameValidationError(folder.Name, nameof(UpdateFolderCommand.Name)));
            }

            return Result.Invalid(folderWritePolicy.CreateDuplicateFolderConstraintValidationError(
                folder.Name,
                folder.UrlSlug,
                nameof(UpdateFolderCommand.Name)));
        }

        await mediator.Publish(new FolderUpdatedEvent(folder), cancellationToken);
        return Result.Success(folder);
    }
}
