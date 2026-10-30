using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Modules.Audience.Features;

/// <summary>
/// Confirms a tenant context and that the form exists in it before audience work runs.
/// </summary>
internal static class TenantFormGate
{
    public static async Task<Result> EnsureAsync(FormGateRequest request)
    {
        var tenant = EnsureTenant(request.TenantId);
        if (!tenant.IsSuccess)
        {
            return tenant;
        }

        Form? form = await request.Forms.GetByIdAsync(request.FormId, request.CancellationToken);
        return form is null
            ? Result.NotFound("Form not found.")
            : Result.Success();
    }

    /// <summary>The tenant half alone, for work that reads the form itself.</summary>
    public static Result EnsureTenant(long tenantId) =>
        tenantId <= 0
            ? Result.Unauthorized("Tenant context is required.")
            : Result.Success();
}

/// <summary>
/// Inputs for <see cref="TenantFormGate.EnsureAsync"/>.
/// </summary>
internal sealed record FormGateRequest(
    IRepository<Form> Forms,
    long TenantId,
    long FormId,
    CancellationToken CancellationToken);
