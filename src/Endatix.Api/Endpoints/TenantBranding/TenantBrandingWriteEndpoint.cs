using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Endatix.Api.Common.FeatureFlags;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.TenantBranding;
using Endatix.Core.UseCases.Tenants.GetById;
using Endatix.Framework.FeatureFlags;
using Endatix.Infrastructure.Caching;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Api.Endpoints.TenantBranding;

/// <summary>A branding write: it names the version it read in <c>If-Match</c> (ADR 0002).</summary>
[Experimental("ENDATIX0002")]
public interface ITenantBrandingWriteRequest
{
    /// <summary>The <c>If-Match</c> header: the quoted <see cref="TenantBrandingModel.Version"/> the client read.</summary>
    string? IfMatch { get; }
}

/// <summary>
/// The steps every branding write shares: read the <c>If-Match</c> version, send the command, then on success evict the
/// tenant's cached public read and return the new <c>ETag</c>. A stale version is 412; a missing one is 428.
/// </summary>
/// <remarks>
/// Every <see cref="ResultStatus.Conflict"/> the branding commands return is a stale version or a lost race, so all of
/// them are 412 here, not the 409 a conflict is elsewhere.
/// </remarks>
[Experimental("ENDATIX0002")]
public abstract class TenantBrandingWriteEndpoint<TRequest>(
    IMediator mediator,
    HybridCache cache,
    ITenantContext tenantContext,
    IUserContext userContext)
    : Endpoint<TRequest, Results<Ok<TenantBrandingModel>, ProblemHttpResult>>
    where TRequest : ITenantBrandingWriteRequest, new()
{
    /// <summary>The command this write sends, given the version the client read and the caller's user id.</summary>
    protected abstract IRequest<Result<TenantBrandingDto>> Command(TRequest request, long expectedVersion, long? userId);

    /// <summary>The permission, the feature flag, and the responses every branding write shares.</summary>
    protected void ConfigureWrite(string summary, string description)
    {
        Permissions(Actions.Tenant.ManageSettings);
        FeatureFlag<EndpointFeatureGate>(FeatureFlags.WhiteLabeling);
        Summary(s =>
        {
            s.Summary = summary;
            s.Description = description;
            s.Responses[200] = "The tenant's branding after the write. The ETag header holds the new version.";
            s.Responses[400] = "Invalid request; problems lists each one as {path, code, detail}.";
            s.Responses[403] = "The caller may not manage tenant settings, or the tenant's plan does not include branding.";
            s.Responses[404] = "White-labeling is not enabled on this deployment.";
            s.Responses[412] = "If-Match names a version that is no longer current; read the branding again.";
            s.Responses[428] = "If-Match is missing.";
        });
        Description(builder => builder
            .Produces<TenantBrandingModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<TenantBrandingModel>, ProblemHttpResult>> ExecuteAsync(TRequest req, CancellationToken ct)
    {
        if (TenantBrandingHttp.IfMatchProblem(req.IfMatch, out var expectedVersion) is { } problem)
        {
            return problem;
        }

        var result = await mediator.Send(Command(req, expectedVersion, CallerId()), ct);
        if (!result.IsSuccess)
        {
            return TenantBrandingHttp.ToProblem(result);
        }

        await EvictPublicReadAsync(ct);
        TenantBrandingHttp.SetETag(HttpContext, result.Value.Version);
        return TypedResults.Ok(TenantBrandingModel.MapWrite(result.Value));
    }

    private long? CallerId() =>
        long.TryParse(userContext.GetCurrentUserId(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    // The public read is cached by short URL, which the write does not know, so it asks for it.
    private async Task EvictPublicReadAsync(CancellationToken ct)
    {
        var tenant = await mediator.Send(new GetTenantByIdQuery(tenantContext.TenantId), ct);
        if (tenant.IsSuccess)
        {
            await cache.RemoveAsync(PublicTenantCacheKeys.Entry(tenant.Value.ShortUrl), ct);
        }
    }
}

/// <summary>The HTTP shapes branding shares: the <c>If-Match</c> version, the <c>ETag</c>, and the problem list.</summary>
internal static class TenantBrandingHttp
{
    internal const string PROBLEMS_KEY = "problems";

    /// <summary>
    /// Reads <c>If-Match: "&lt;version&gt;"</c> (the quotes may be left out). Null when it holds one version; otherwise
    /// 428 when it is missing and 400 when it is a weak tag, <c>*</c>, a list, or not a number.
    /// </summary>
    internal static ProblemHttpResult? IfMatchProblem(string? ifMatch, out long expectedVersion)
    {
        expectedVersion = 0;
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return Problem(StatusCodes.Status428PreconditionRequired, "Precondition required",
                "Send If-Match with the version you read, in quotes; \"0\" before the first publish.");
        }

        var value = ifMatch.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out expectedVersion)
            ? null
            : Problem(StatusCodes.Status400BadRequest, Infrastructure.ResultExtensions.ResultTitles.BAD_REQUEST,
                "If-Match must hold one version number in quotes, such as \"3\".");
    }

    /// <summary>A refused branding write: a conflict is 412, an invalid request lists its problems.</summary>
    internal static ProblemHttpResult ToProblem(Core.Infrastructure.Result.IResult result)
    {
        if (result.Status == ResultStatus.Conflict)
        {
            return Problem(StatusCodes.Status412PreconditionFailed, "Precondition failed", string.Join('\n', result.Errors));
        }

        var problem = result.ToProblem();
        if (result.IsInvalid())
        {
            problem.ProblemDetails.Extensions[PROBLEMS_KEY] = result.ValidationErrors
                .Select(error => new BrandingProblem(error.Identifier ?? "", error.ErrorCode ?? "", error.ErrorMessage))
                .ToArray();
        }

        return problem;
    }

    internal static void SetETag(HttpContext httpContext, long version) =>
        httpContext.Response.Headers.ETag = $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    private static ProblemHttpResult Problem(int status, string title, string detail) =>
        TypedResults.Problem(EndatixProblemDetails.Create(status, title, detail));
}
