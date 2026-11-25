using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases;

internal sealed class ContentQueryScope
{
    internal static ContentQueryScope Repository { get; } = new();
    private readonly ITenantContext? _tenant;

    private ContentQueryScope() { }
    public ContentQueryScope(ITenantContext tenant) => _tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));

    internal long? TenantId => _tenant?.TenantId;

    internal void Apply<T>(Specification<T> spec) where T : TenantEntity => spec.InTenant(_tenant);

    internal Task<T?> FindAsync<T>(IRepository<T> rows, long id, CancellationToken ct) where T : TenantEntity, IAggregateRoot =>
        _tenant is null ? rows.GetByIdAsync(id, ct) : rows.FindAsync(id, _tenant, ct);

    internal TSpec Submission<TSpec>(TSpec spec) where TSpec : Specification<Submission> =>
        _tenant is null ? spec : spec.OnLiveForm(_tenant.TenantId);
}
