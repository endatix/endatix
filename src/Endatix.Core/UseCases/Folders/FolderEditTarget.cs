using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Folders;

internal sealed record FolderEditTarget(bool Immutable, string FallbackName)
{
    internal sealed class ById : SingleResultSpecification<Folder, FolderEditTarget>
    {
        internal ById(long id, long tenantId, bool needsName = false) =>
            Query.Where(f => tenantId > 0 && f.TenantId == tenantId && f.Id == id)
                .Select(f => new FolderEditTarget(f.Immutable, needsName ? f.Name : ""));
    }
}
