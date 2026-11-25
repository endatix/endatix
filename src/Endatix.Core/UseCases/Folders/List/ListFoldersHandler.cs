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
public sealed class ListFoldersHandler(IRepository<Folder> repository, ITenantContext tenantContext, ManagedContentScope? scope)
    : IQueryHandler<ListFoldersQuery, Result<IEnumerable<FolderDto>>>
{
    /// <summary>Constructs the ordinary operation without account standing enforcement.</summary>
    public ListFoldersHandler(IRepository<Folder> repository, ITenantContext tenantContext)
        : this(repository, tenantContext, null) { }

    /// <inheritdoc/>
    public async Task<Result<IEnumerable<FolderDto>>> Handle(ListFoldersQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);

        if (scope is not null && await scope.RefusalAsync<IEnumerable<FolderDto>>(cancellationToken) is { } refusal)
        {
            return refusal;
        }

        Specification<Folder> spec = request.IncludeInactive
        ? new FolderSpecifications.AllFoldersSpec()
        : new FolderSpecifications.ActiveFoldersSpec();

        spec.InTenant(scope);
        var folders = await repository.ListAsync(spec, cancellationToken);

        return Result.Success(folders.Select(FolderDto.From));
    }
}
