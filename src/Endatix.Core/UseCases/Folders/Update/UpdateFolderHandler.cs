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
        if (folderWritePolicy.Scope is { IsEnforced: true } scope &&
            await RefusalAsync(scope, request, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        return await UpdateAsync(request, cancellationToken);
    }

    private async Task<Result<Folder>> UpdateAsync(UpdateFolderCommand request, CancellationToken cancellationToken)
    {
        var spec = new FolderSpecifications.FolderByIdSpec(request.FolderId);
        spec.InTenant(folderWritePolicy.Scope);
        var folder = await folderRepository.FirstOrDefaultAsync(spec, cancellationToken);
        if (folder is null)
        {
            return Result.NotFound("Folder not found.");
        }

        var edit = await PrepareAsync(new(folder.Immutable, folder.Name), request, cancellationToken);
        if (!edit.IsSuccess)
        {
            return edit.ToErrorResult<Folder>();
        }

        edit.Value.Apply(folder, request);
        return await SaveAndPublishAsync(folder, cancellationToken);
    }

    private async Task<Result<Folder>?> RefusalAsync(ManagedContentScope scope, UpdateFolderCommand request, CancellationToken ct)
    {
        var needsName = request.Name is null && request.Slug is not null && string.IsNullOrWhiteSpace(request.Slug);
        var target = await folderRepository.FirstOrDefaultAsync(new FolderEditTarget.ById(request.FolderId, scope.TenantId, needsName), ct);
        if (target is null)
        {
            return Result.NotFound("Folder not found.");
        }
        var edit = await PrepareAsync(target, request, ct);
        return edit.IsSuccess ? await scope.RefusalAsync<Folder>(ct) : edit.ToErrorResult<Folder>();
    }

    private async Task<Result<Edit>> PrepareAsync(FolderEditTarget target, UpdateFolderCommand request, CancellationToken ct)
    {
        if (target.Immutable && request.Immutable is not false &&
            Folder.ChangesMutableFields((request.Name, request.Slug, request.Description, request.Metadata)))
        {
            return Result.Invalid(folderWritePolicy.CreateImmutableFolderValidationError(nameof(UpdateFolderCommand.FolderId)));
        }
        var name = await NameAsync(request, ct);
        if (!name.IsSuccess)
        {
            return name.ToErrorResult<Edit>();
        }
        var slug = await SlugAsync(request, name.Value?.Name ?? target.FallbackName, ct);
        return slug.IsSuccess ? new Edit(name.Value, slug.Value) : slug.ToErrorResult<Edit>();
    }

    private async Task<Result<(string Name, string NormalizedName)?>> NameAsync(UpdateFolderCommand request, CancellationToken ct)
    {
        if (request.Name is null)
        {
            return Result<(string, string)?>.Success(null);
        }

        var nameResult = folderWritePolicy.NormalizeNameOrError(request.Name);
        if (!nameResult.IsSuccess)
        {
            return Result.Invalid(folderWritePolicy.CreateNameNormalizationValidationError(nameof(UpdateFolderCommand.Name)));
        }

        var name = nameResult.Value;
        return await folderWritePolicy.NameExistsAsync(name.NormalizedName, request.FolderId, ct)
            ? Result.Invalid(folderWritePolicy.CreateDuplicateNameValidationError(name.Name, nameof(UpdateFolderCommand.Name)))
            : Result<(string, string)?>.Success(name);
    }

    private async Task<Result<string?>> SlugAsync(UpdateFolderCommand request, string fallback, CancellationToken ct)
    {
        if (request.Slug is null)
        {
            return Result<string?>.Success(null);
        }

        var slugResult = FolderWritePolicy.NormalizeAndValidateSlugOrError(request.Slug, fallback, includeDetailedInvalidMessage: false);
        if (!slugResult.IsSuccess || slugResult.Value is null)
        {
            var message = slugResult.Errors.FirstOrDefault() ?? "Slug is invalid.";
            return Result.Invalid(folderWritePolicy.CreateInvalidSlugValidationError(message, nameof(UpdateFolderCommand.Slug)));
        }

        return await folderWritePolicy.SlugExistsAsync(slugResult.Value, request.FolderId, ct)
            ? Result.Invalid(folderWritePolicy.CreateDuplicateSlugValidationError(slugResult.Value, nameof(UpdateFolderCommand.Slug)))
            : Result<string?>.Success(slugResult.Value);
    }

    private sealed record Edit((string Name, string NormalizedName)? Name, string? Slug)
    {
        internal void Apply(Folder folder, UpdateFolderCommand request)
        {
            if (Name is { } name)
            {
                (folder.Name, folder.NormalizedName) = name;
            }
            folder.UrlSlug = Slug ?? folder.UrlSlug;
            ApplyTextFields(folder, request);
            ApplyStateFields(folder, request);
        }
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

        if (request.Immutable.HasValue)
        {
            folder.Immutable = request.Immutable.Value;
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
            var mapped = folderWritePolicy.UniqueViolation(folder, uniqueConstraintViolationChecker.AnalyzeUniqueConstraint(exception));
            if (mapped is null)
            {
                throw;
            }

            return mapped;
        }

        await mediator.Publish(new FolderUpdatedEvent(folder), cancellationToken);
        return Result.Success(folder);
    }

}
