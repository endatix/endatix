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
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        Form? form = await request.Forms.GetByIdAsync(request.FormId, request.CancellationToken);
        return form is null
            ? Result.NotFound("Form not found.")
            : Result.Success();
    }
}

/// <summary>
/// Inputs for <see cref="TenantFormGate.EnsureAsync"/>.
/// </summary>
internal sealed record FormGateRequest(
    IRepository<Form> Forms,
    long TenantId,
    long FormId,
    CancellationToken CancellationToken);
