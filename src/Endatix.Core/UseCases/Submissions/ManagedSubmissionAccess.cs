using System.Diagnostics.CodeAnalysis;
using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>Checks managed targets without materializing answers, schemas or attached files.</summary>
[Experimental("ENDATIX0006")]
public sealed class ManagedSubmissionAccess(ITenantContext tenant, AccountStanding standing,
    IRepository<Submission> submissions, IRepository<Form> forms)
{
    /// <summary>A visible submission on its live form; the caller retains its normal read or write-time checks.</summary>
    public async Task<Result?> SubmissionAsync((long Form, long Submission) id, string missing, CancellationToken ct)
    {
        if (!standing.IsEnforced)
        {
            return null;
        }
        var spec = new SubmissionByFormIdAndSubmissionIdSpec(id.Form, id.Submission);
        spec.Query.Where(s => s.TenantId == tenant.TenantId && s.Form.TenantId == tenant.TenantId);
        return tenant.TenantId <= 0 || !await submissions.AnyAsync(spec, ct)
            ? Result.NotFound(missing)
            : await standing.RefusalAsync(ct);
    }

    /// <summary>Test marking distinguishes a missing form from a missing submission.</summary>
    public async Task<Result?> TestTargetAsync((long Form, long Submission) id, CancellationToken ct)
    {
        if (!standing.IsEnforced)
        {
            return null;
        }
        if (tenant.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }
        if (!await forms.AnyAsync(new FormSpecifications.ByIdForTenant(id.Form, tenant.TenantId), ct))
        {
            return Result.NotFound("Form not found");
        }
        return await submissions.AnyAsync(new SubmissionByIdForTenantSpec(tenant.TenantId, id.Form, id.Submission), ct)
            ? await standing.RefusalAsync(ct)
            : Result.NotFound("Submission not found");
    }

    internal async Task<Result<Paged<SubmissionDto>>?> ListAsync(IRepository<FormDefinition> definitions, long form, CancellationToken ct)
    {
        var spec = new FormDefinitionsByFormIdSpec(form);
        spec.Query.Where(d => d.TenantId == tenant.TenantId);
        return tenant.TenantId <= 0 || !await definitions.AnyAsync(spec, ct)
            ? Result.NotFound("Form not found.")
            : await standing.RefusalAsync<Paged<SubmissionDto>>(ct);
    }

    internal bool IsEnforced => standing.IsEnforced;

    internal async Task<Result?> TokenAsync((long Form, long Submission) id, CancellationToken ct)
    {
        var spec = new SubmissionWithDefinitionSpec(id.Form, id.Submission);
        spec.Query.Where(s => s.TenantId == tenant.TenantId && s.FormDefinition.TenantId == tenant.TenantId);
        return tenant.TenantId <= 0 || !await submissions.AnyAsync(spec, ct)
            ? Result.NotFound("Submission not found")
            : await standing.RefusalAsync(ct);
    }
}
