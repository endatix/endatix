using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Shared;

namespace Endatix.Modules.Personalization.Features;

/// <summary>
/// Shared tenant + form existence gate for audience commands.
/// </summary>
internal static class TenantFormGate
{
    public static async Task<Result> EnsureAsync(FormGateRequest request)
    {
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        Result formResult = await FormAudienceGuard.EnsureFormExistsAsync(
            request.Forms, request.FormId, request.CancellationToken);
        return formResult.IsSuccess
            ? Result.Success()
            : Result.NotFound(formResult.Errors.ToArray());
    }

    public static Result<T> MapFailure<T>(IResult failed) =>
        failed.Status switch
        {
            ResultStatus.Unauthorized => Result.Unauthorized(failed.Errors.ToArray()),
            ResultStatus.NotFound => Result.NotFound(failed.Errors.ToArray()),
            ResultStatus.Invalid => Result.Invalid(failed.ValidationErrors.ToArray()),
            ResultStatus.Conflict => Result.Conflict(failed.Errors.ToArray()),
            _ => Result.Error(new ErrorList(failed.Errors)),
        };
}

/// <summary>
/// Inputs for <see cref="TenantFormGate.EnsureAsync"/>.
/// </summary>
internal sealed record FormGateRequest(
    IRepository<Form> Forms,
    long TenantId,
    long FormId,
    CancellationToken CancellationToken);
