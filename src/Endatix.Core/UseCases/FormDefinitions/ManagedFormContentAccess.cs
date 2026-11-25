using System.Diagnostics.CodeAnalysis;
using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.FormDefinitions;

/// <summary>Resolves managed content visibility without loading the schema or settings.</summary>
[Experimental("ENDATIX0006")]
public sealed class ManagedFormContentAccess(
    ITenantContext tenant, AccountStanding standing, IRepository<Form> forms, IRepository<FormDefinition> definitions)
{
    internal Task<Result<T>?> FormAsync<T>(long id, CancellationToken ct) =>
        CheckAsync<T>(() => forms.AnyAsync(Form(id), ct), "Form not found.", ct);

    internal Task<Result<FormDefinition>?> ActiveAsync(long id, CancellationToken ct) =>
        CheckAsync<FormDefinition>(() =>
        {
            var form = Form(id);
            form.Query.Where(f => f.ActiveDefinition != null && f.ActiveDefinition.TenantId == tenant.TenantId);
            return forms.AnyAsync(form, ct);
        }, "Active form definition not found.", ct);

    internal Task<Result<FormDefinition>?> DefinitionOnFormAsync(long id, long definitionId, CancellationToken ct) =>
        CheckAsync<FormDefinition>(() => forms.AnyAsync(new DefinitionByFormAndDefinitionIdSpec(id, definitionId, tenant.TenantId), ct),
            "Form definition not found.", ct);

    internal Task<Result<FormDefinition>?> DefinitionAsync(long id, long definitionId, CancellationToken ct) =>
        CheckAsync<FormDefinition>(() =>
        {
            var spec = new FormDefinitionsListFilterSpec(id);
            spec.Query.Where(d => d.Id == definitionId && d.TenantId == tenant.TenantId);
            return definitions.AnyAsync(spec, ct);
        }, "Form definition not found.", ct);

    internal Task<Result<Paged<FormDefinition>>?> ListAsync(FormDefinitionsListFilterSpec filter, CancellationToken ct) =>
        CheckAsync<Paged<FormDefinition>>(() =>
        {
            filter.Query.Where(d => d.TenantId == tenant.TenantId);
            return definitions.AnyAsync(filter, ct);
        }, "Form not found.", ct);

    private FormSpecifications.ByIdForTenant Form(long id) => new(id, tenant.TenantId);

    private async Task<Result<T>?> CheckAsync<T>(Func<Task<bool>> visible, string missing, CancellationToken ct)
    {
        if (!standing.IsEnforced)
        {
            return null;
        }
        return tenant.TenantId <= 0 || !await visible()
            ? Result<T>.NotFound(missing)
            : await standing.RefusalAsync<T>(ct);
    }
}
