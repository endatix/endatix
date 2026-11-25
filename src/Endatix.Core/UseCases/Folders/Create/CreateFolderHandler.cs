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
        var existingByNormalizedName = await folderWritePolicy.NameExistsAsync(
            normalizedName,
            excludeFolderId: null,
            cancellationToken);
        if (existingByNormalizedName)
        {
            return Result.Invalid(folderWritePolicy.CreateDuplicateNameValidationError(trimmedName, nameof(CreateFolderCommand.Name)));
        }

        var slugResult = FolderWritePolicy.NormalizeAndValidateSlugOrError(request.Slug, trimmedName, includeDetailedInvalidMessage: true);
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

        if (folderWritePolicy.Scope is { } scope && await scope.RefusalAsync<Folder>(cancellationToken) is { } refusal)
        {
            return refusal;
        }

        return await CreateAsync(new Folder(
            tenantContext.TenantId,
            trimmedName,
            baseSlug,
            normalizedName,
            request.Description?.Trim(),
            null,
            request.Metadata)
        { Immutable = request.Immutable }, cancellationToken);
    }

    private async Task<Result<Folder>> CreateAsync(Folder folder, CancellationToken cancellationToken)
    {
        try
        {
            await folderRepository.AddAsync(folder, cancellationToken);
        }
        catch (Exception exception)
        {
            var violation = uniqueConstraintViolationChecker.AnalyzeUniqueConstraint(exception);
            if (folderWritePolicy.UniqueViolation(folder, violation) is not { } invalid)
            {
                throw;
            }

            return invalid;
        }

        await mediator.Publish(new FolderCreatedEvent(folder), cancellationToken);
        return Result<Folder>.Created(folder);
    }

}
