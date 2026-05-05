using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Folders.GetBySlug;

/// <summary>
/// Handler for getting a folder by slug
/// </summary>
public sealed class GetFolderBySlugHandler(IRepository<Folder> repository, ITenantContext tenantContext)
    : IQueryHandler<GetFolderBySlugQuery, Result<FolderDto>>
{
    /// <inheritdoc/>
    public async Task<Result<FolderDto>> Handle(GetFolderBySlugQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);
        Guard.Against.NullOrWhiteSpace(request.Slug);

        var spec = new FolderSpecifications.FolderBySlugSpec(request.Slug);
        var folder = await repository.SingleOrDefaultAsync(spec, cancellationToken);
        if (folder is null)
        {
            return Result.NotFound("Folder not found.");
        }

        var dto = new FolderDto
        {
            Id = folder.Id,
            Name = folder.Name,
            Slug = folder.UrlSlug,
            Description = folder.Description,
            Metadata = folder.Metadata,
            IsActive = folder.IsActive,
            Immutable = folder.Immutable,
            CreatedAt = folder.CreatedAt,
            ModifiedAt = folder.ModifiedAt
        };

        return Result.Success(dto);
    }
}
