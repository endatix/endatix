using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions;

internal sealed class SubmissionTestForms(IRepository<Form> forms, ContentQueryScope scope)
{
    internal bool Allows(long tenant) => tenant > 0 && (scope.TenantId is not long current || current == tenant);

    internal Task<bool> ExistsAsync(long tenant, long form, CancellationToken ct) =>
        forms.AnyAsync(new FormSpecifications.ByIdForTenant(form, tenant), ct);

    internal Task<Form?> ReadAsync(long tenant, long form, CancellationToken ct) =>
        forms.FirstOrDefaultAsync(new FormSpecifications.ByIdForTenant(form, tenant), ct);
}
