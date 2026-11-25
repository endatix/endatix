using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.UseCases;

internal sealed class EditableContent<T>(IRepository<T> rows, ContentQueryScope scope) where T : TenantEntity, IAggregateRoot
{
    internal Task<T?> FindAsync(long id, CancellationToken ct) => scope.FindAsync(rows, id, ct);
    internal Task UpdateAsync(T entity, CancellationToken ct) => rows.UpdateAsync(entity, ct);
}
