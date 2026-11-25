using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Endatix.Api.Common;
using Endatix.Api.Common.FeatureFlags;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.TenantBranding;
using Endatix.Core.UseCases.TenantBranding.Get;
using Endatix.Framework.FeatureFlags;
using Endatix.Infrastructure.Caching;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.TenantBranding;

/// <summary>
/// The steps every branding write shares. It names the version it read in <c>If-Match</c> (<see cref="EntityTagHeader"/>,
/// the pattern pending ADR 0006): 428 when that is missing, 400 when it is not one strong tag, 412 with the current
/// version when it is stale. On success the tenant's
/// cached public read is evicted and the new version is the <c>ETag</c>.
/// </summary>
/// <remarks>
/// <see cref="Configure"/> is sealed, so every write carries the permission and the feature flag. Every
/// <see cref="ResultStatus.Conflict"/> the branding commands return is a stale version or a lost race, so all of them are
/// 412 here; the shared <c>ToProblem</c> keeps a conflict as 409.
/// </remarks>
[Experimental("ENDATIX0002")]
public abstract class TenantBrandingWriteEndpoint<TRequest>(
    IMediator mediator,
    IPublicTenantCacheInvalidator publicTenantCache,
    IUserContext userContext)
    : Endpoint<TRequest, Results<Ok<TenantBrandingModel>, ProblemHttpResult>>
    where TRequest : notnull
{
    /// <summary>The verb and route, and what the write does.</summary>
    protected abstract WriteRoute Route { get; }

    /// <summary>The command this write sends, given the version the client read and the caller's user id.</summary>
    protected abstract IRequest<Result<TenantBrandingDto>> Command(TRequest request, long expectedVersion, long? userId);

    /// <summary>Extra configuration a write needs, such as how its body binds.</summary>
    protected virtual void ConfigureBody() { }

    /// <inheritdoc/>
    public sealed override void Configure()
    {
        Verbs(Route.Verb);
        Routes(Route.Path);
        Permissions(Actions.Tenant.ManageSettings);
        FeatureFlag<EndpointFeatureGate>(FeatureFlags.WhiteLabeling);
        Tags(FeatureFlags.WhiteLabeling);
        ConfigureBody();
        Summary(s =>
        {
            s.Summary = Route.Summary;
            s.Description = Route.Description;
            s.Responses[200] = "The tenant's branding after the write. The ETag header holds the new version.";
            s.Responses[400] = "Invalid request or If-Match; problems lists each refusal as {path, code, detail}.";
            s.Responses[403] = "The caller may not manage tenant settings, or the tenant's plan does not include branding.";
            s.Responses[404] = "White-labeling is not enabled on this deployment.";
            s.Responses[412] = "If-Match names a version that is no longer current; the ETag header and version hold the " +
                "current one. A write whose content is already live succeeds whatever If-Match names.";
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
        if (EntityTagHeader.ReadIfMatch(HttpContext.Request.Headers.IfMatch.ToString(), out var expectedVersion) is { } refused)
        {
            return refused;
        }

        var result = await mediator.Send(Command(req, expectedVersion, CallerId()), ct);
        if (result.Status == ResultStatus.Conflict)
        {
            return await StaleAsync(result, ct);
        }

        return result.IsSuccess
            ? await SavedAsync(result.Value, expectedVersion)
            : TenantBrandingHttp.ToProblem(result);
    }

    private async Task<Results<Ok<TenantBrandingModel>, ProblemHttpResult>> SavedAsync(
        TenantBrandingDto branding,
        long expectedVersion)
    {
        // A write that changed nothing, named at the current version, keeps the cached read. One that names an older
        // version also evicts it, which is harmless. The write is committed, so a client that hangs up now must not
        // leave the old brand cached.
        if (branding.Version != expectedVersion)
        {
            await publicTenantCache.InvalidateCurrentTenantAsync(CancellationToken.None);
        }

        EntityTagHeader.SetETag(HttpContext.Response, branding.Version);
        return TypedResults.Ok(TenantBrandingModel.MapWrite(branding));
    }

    // A stale write names the current version, so the client can read it and retry (ARCHITECTURE.md, If-Match row).
    private async Task<ProblemHttpResult> StaleAsync(Result<TenantBrandingDto> result, CancellationToken ct)
    {
        var problem = TenantBrandingHttp.Problem(StatusCodes.Status412PreconditionFailed, "Precondition failed",
            string.Join('\n', result.Errors));
        var current = await mediator.Send(new GetTenantBrandingQuery(), ct);
        if (current.IsSuccess)
        {
            var version = current.Value.Branding.Version;
            EntityTagHeader.SetETag(HttpContext.Response, version);
            problem.ProblemDetails.Extensions["version"] = version.ToString(CultureInfo.InvariantCulture);
        }

        return problem;
    }

    private long? CallerId() =>
        long.TryParse(userContext.GetCurrentUserId(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    /// <summary>A branding write's verb and route, and the text its OpenAPI entry shows.</summary>
    /// <param name="Verb">The HTTP verb.</param>
    /// <param name="Path">The route, under the API prefix.</param>
    /// <param name="Summary">The OpenAPI summary.</param>
    /// <param name="Description">The OpenAPI description.</param>
    protected sealed record WriteRoute(Http Verb, string Path, string Summary, string Description);
}

/// <summary>The HTTP shapes branding shares: the <c>ETag</c> and the problem list.</summary>
internal static class TenantBrandingHttp
{
    /// <summary>A refused branding request; an invalid one lists its problems as <c>{path, code, detail}</c>.</summary>
    internal static ProblemHttpResult ToProblem(Core.Infrastructure.Result.IResult result)
    {
        var problem = result.ToProblem();
        if (result.IsInvalid())
        {
            problem.ProblemDetails.Extensions["problems"] = result.ValidationErrors
                .Select(error => new BrandingProblem(error.Identifier ?? "", error.ErrorCode ?? "", error.ErrorMessage))
                .ToArray();
        }

        return problem;
    }

    internal static ProblemHttpResult Problem(int status, string title, string detail) =>
        TypedResults.Problem(EndatixProblemDetails.Create(status, title, detail));
}
