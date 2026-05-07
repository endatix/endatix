using Ardalis.GuardClauses;
using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Folders.List;

/// <summary>
/// Handler for listing folders.
/// </summary>
public sealed class ListFoldersHandler(IRepository<Folder> repository, ITenantContext tenantContext)
    : IQueryHandler<ListFoldersQuery, Result<IEnumerable<FolderDto>>>
{
    /// <inheritdoc/>
    public async Task<Result<IEnumerable<FolderDto>>> Handle(ListFoldersQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);

        IEnumerable<Folder> folders;
        Specification<Folder> spec = request.IncludeInactive
        ? new FolderSpecifications.AllFoldersSpec()
        : new FolderSpecifications.ActiveFoldersSpec();

        folders = await repository.ListAsync(spec, cancellationToken);

        var dtos = folders.Select(f => new FolderDto
        {
            Id = f.Id,
            Name = f.Name,
            Slug = f.UrlSlug,
            Description = f.Description,
            Metadata = f.Metadata,
            IsActive = f.IsActive,
            Immutable = f.Immutable,
            CreatedAt = f.CreatedAt,
            ModifiedAt = f.ModifiedAt
        });

        return Result.Success(dtos);
    }
}
