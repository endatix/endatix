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
public sealed class GetFolderBySlugHandler(IRepository<Folder> repository, ITenantContext tenantContext, ManagedContentScope? scope)
    : IQueryHandler<GetFolderBySlugQuery, Result<FolderDto>>
{
    /// <summary>Constructs the ordinary operation without account standing enforcement.</summary>
    public GetFolderBySlugHandler(IRepository<Folder> repository, ITenantContext tenantContext)
        : this(repository, tenantContext, null) { }

    /// <inheritdoc/>
    public async Task<Result<FolderDto>> Handle(GetFolderBySlugQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);
        Guard.Against.NullOrWhiteSpace(request.Slug);

        var spec = new FolderSpecifications.FolderBySlugSpec(request.Slug);
        spec.InTenant(scope);
        if (scope is { IsEnforced: true })
        {
            if (!await repository.AnyAsync(spec, cancellationToken))
            {
                return Result.NotFound("Folder not found.");
            }
            if (await scope.RefusalAsync<FolderDto>(cancellationToken) is { } refusal)
            {
                return refusal;
            }
        }
        return await ReadAsync(spec, cancellationToken);
    }

    private async Task<Result<FolderDto>> ReadAsync(FolderSpecifications.FolderBySlugSpec spec, CancellationToken cancellationToken)
    {
        var folder = await repository.SingleOrDefaultAsync(spec, cancellationToken);
        if (folder is null)
        {
            return Result.NotFound("Folder not found.");
        }

        return FolderDto.From(folder);
    }
}
