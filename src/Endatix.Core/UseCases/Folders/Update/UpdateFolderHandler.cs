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

        var changed = await ApplyChangesAsync(folder, request, cancellationToken);
        if (!changed.IsSuccess)
        {
            return changed;
        }

        return await SaveAndPublishAsync(folder, cancellationToken);
    }

    private async Task<Result<Folder>> ApplyChangesAsync(Folder folder, UpdateFolderCommand request, CancellationToken cancellationToken)
    {
        if (!folder.CanModifyMutableState(request.Name, request.Slug, request.Description, request.Metadata))
        {
            return Result.Invalid(folderWritePolicy.CreateImmutableFolderValidationError(nameof(UpdateFolderCommand.FolderId)));
        }

        return await ApplyIdentityAsync(folder, request, cancellationToken);
    }

    private async Task<Result<Folder>> ApplyIdentityAsync(Folder folder, UpdateFolderCommand request, CancellationToken cancellationToken)
    {
        var nameError = await ApplyNameAsync(folder, request.Name, cancellationToken);
        if (nameError is not null)
        {
            return nameError;
        }

        var slugError = await ApplySlugAsync(folder, request.Slug, cancellationToken);
        if (slugError is not null)
        {
            return slugError;
        }

        ApplyTextFields(folder, request);
        ApplyStateFields(folder, request);
        return Result.Success(folder);
    }

    private async Task<Result<Folder>?> ApplyNameAsync(Folder folder, string? name, CancellationToken cancellationToken)
    {
        if (name is null)
        {
            return null;
        }

        var nameResult = folderWritePolicy.NormalizeNameOrError(name);
        if (!nameResult.IsSuccess)
        {
            return Result.Invalid(folderWritePolicy.CreateNameNormalizationValidationError(nameof(UpdateFolderCommand.Name)));
        }

        return await AssignNameAsync(folder, nameResult.Value, cancellationToken);
    }

    private async Task<Result<Folder>?> AssignNameAsync(Folder folder, (string Trimmed, string Normalized) name, CancellationToken cancellationToken)
    {
        var nameTaken = await FolderWritePolicy.NormalizedNameExistsAsync(folderRepository, name.Normalized, folder.Id, cancellationToken);
        if (nameTaken)
        {
            return Result.Invalid(folderWritePolicy.CreateDuplicateNameValidationError(name.Trimmed, nameof(UpdateFolderCommand.Name)));
        }

        folder.Name = name.Trimmed;
        folder.NormalizedName = name.Normalized;
        return null;
    }

    private async Task<Result<Folder>?> ApplySlugAsync(Folder folder, string? slug, CancellationToken cancellationToken)
    {
        if (slug is null)
        {
            return null;
        }

        var slugResult = FolderWritePolicy.NormalizeAndValidateSlugOrError(slug, folder.Name, includeDetailedInvalidMessage: false);
        if (!slugResult.IsSuccess || slugResult.Value is null)
        {
            var message = slugResult.Errors.FirstOrDefault() ?? "Slug is invalid.";
            return Result.Invalid(folderWritePolicy.CreateInvalidSlugValidationError(message, nameof(UpdateFolderCommand.Slug)));
        }

        return await AssignSlugAsync(folder, slugResult.Value, cancellationToken);
    }

    private async Task<Result<Folder>?> AssignSlugAsync(Folder folder, string normalized, CancellationToken cancellationToken)
    {
        var slugTaken = await folderWritePolicy.SlugExistsAsync(normalized, folder.Id, cancellationToken);
        if (slugTaken)
        {
            return Result.Invalid(folderWritePolicy.CreateDuplicateSlugValidationError(normalized, nameof(UpdateFolderCommand.Slug)));
        }

        folder.UrlSlug = normalized;
        return null;
    }

    private static void ApplyTextFields(Folder folder, UpdateFolderCommand request)
    {
        if (request.Description is not null)
        {
            folder.Description = request.Description.Trim();
        }

        if (request.Metadata is not null)
        {
            folder.Metadata = request.Metadata;
        }
    }

    private static void ApplyStateFields(Folder folder, UpdateFolderCommand request)
    {
        if (request.IsActive.HasValue)
        {
            folder.IsActive = request.IsActive.Value;
        }

        if (request.Immutable is true)
        {
            folder.Immutable = true;
        }
    }

    private async Task<Result<Folder>> SaveAndPublishAsync(Folder folder, CancellationToken cancellationToken)
    {
        try
        {
            await folderRepository.UpdateAsync(folder, cancellationToken);
        }
        catch (Exception exception)
        {
            var mapped = MapUniqueViolation(folder, exception);
            if (mapped is null)
            {
                throw;
            }

            return mapped;
        }

        await mediator.Publish(new FolderUpdatedEvent(folder), cancellationToken);
        return Result.Success(folder);
    }

    private Result<Folder>? MapUniqueViolation(Folder folder, Exception exception)
    {
        var violation = uniqueConstraintViolationChecker.AnalyzeUniqueConstraint(exception);
        if (!violation.IsUniqueConstraintViolation)
        {
            return null;
        }

        if (violation.IsFolderSlugViolation())
        {
            return Result.Invalid(folderWritePolicy.CreateDuplicateSlugValidationError(folder.UrlSlug, nameof(UpdateFolderCommand.Slug)));
        }

        if (violation.IsFolderNameViolation())
        {
            return Result.Invalid(folderWritePolicy.CreateDuplicateNameValidationError(folder.Name, nameof(UpdateFolderCommand.Name)));
        }

        return Result.Invalid(folderWritePolicy.CreateDuplicateFolderConstraintValidationError(folder.Name, folder.UrlSlug, nameof(UpdateFolderCommand.Name)));
    }
}
