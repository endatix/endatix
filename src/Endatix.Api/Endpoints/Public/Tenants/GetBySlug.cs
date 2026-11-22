using System.Text.Json;
using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Tenants.GetPublicBySlug;
using Endatix.Api.Endpoints.TenantBranding;
using Endatix.Framework.FeatureFlags;
using Endatix.Infrastructure.Caching;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Api.Endpoints.Public.Tenants;

/// <summary>
/// Unauthenticated tenant discovery by opaque public id. Rate-limited; 404s are not cached.
/// </summary>
/// <remarks>
/// The tenant's published branding is cached with the rest and evicted by the branding writes. The <c>WhiteLabeling</c>
/// flag is applied after the cache, so turning it off hides every brand at once.
/// </remarks>
public sealed class GetBySlug(IMediator mediator, HybridCache cache, IFeatureGate featureGate)
    : Endpoint<GetPublicTenantRequest, Results<Ok<PublicTenantModel>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("tenants/{slug}");
        Group<PublicApiGroup>();
        AllowAnonymous();
        Throttle(20, 60);
        Summary(s =>
        {
            s.Summary = "Get public tenant";
            s.Description = "Returns the tenant name, self-registration policy and published branding (an object; null when the " +
                "tenant has none, is not entitled, or white-labeling is off) for an opaque public id. Does not return the numeric tenant id.";
            s.Responses[200] = "Tenant found.";
            s.Responses[404] = "Unknown or deleted public id.";
            s.Responses[429] = "Too many requests.";
        });
        Description(builder => builder
            .Produces<PublicTenantModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<PublicTenantModel>, ProblemHttpResult>> ExecuteAsync(
        GetPublicTenantRequest request,
        CancellationToken ct)
    {
        var normalizedSlug = PublicTenantCacheKeys.TryNormalized(request.Slug);
        if (normalizedSlug is null)
        {
            return TypedResultsBuilder
                .FromResult(Result<PublicTenantModel>.NotFound(GetPublicTenantHandler.TenantNotFoundMessage))
                .SetTypedResults<Ok<PublicTenantModel>, ProblemHttpResult>();
        }

        var result = await cache.GetOrCreateResultAsync(
            PublicTenantCacheKeys.Entry(normalizedSlug),
            token => mediator.Send(new GetPublicTenantQuery(normalizedSlug), token),
            new HybridCacheEntryOptions
            {
                Expiration = PublicTenantCacheKeys.Ttl,
                LocalCacheExpiration = PublicTenantCacheKeys.Ttl
            },
            PublicTenantCacheKeys.TagsFor(normalizedSlug),
            ct);

        var showBranding = result.IsSuccess
            && await featureGate.IsEnabledAsync(FeatureFlags.WhiteLabeling, ct);
        return TypedResultsBuilder
            .MapResult(result, tenant => PublicTenantModel.Map(tenant, showBranding))
            .SetTypedResults<Ok<PublicTenantModel>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request for public tenant discovery.
/// </summary>
public sealed class GetPublicTenantRequest
{
    /// <summary>Opaque 8-character public id (<c>Tenant.ShortUrl</c>).</summary>
    public string Slug { get; set; } = string.Empty;
}

/// <summary>
/// Empty-only. Invalid short URL format is 404 (same as unknown) so we do not leak structure.
/// </summary>
public sealed class GetPublicTenantValidator : Validator<GetPublicTenantRequest>
{
    public GetPublicTenantValidator()
    {
        RuleFor(request => request.Slug).NotEmpty();
    }
}

/// <summary>
/// Public tenant discovery response. The numeric tenant id is omitted on purpose.
/// </summary>
/// <param name="Slug">The opaque public id.</param>
/// <param name="Name">The tenant's display name.</param>
/// <param name="SelfRegistrationEnabled">Whether people may register themselves.</param>
/// <param name="AllowedAuthProviders">The sign-in providers self-registration allows.</param>
/// <param name="Branding">The tenant's published brand override as a JSON object; null when the deployment brand
/// applies.</param>
public sealed record PublicTenantModel(
    string Slug,
    string Name,
    bool SelfRegistrationEnabled,
    IReadOnlyList<string> AllowedAuthProviders,
    JsonElement? Branding = null)
{
    public static PublicTenantModel Map(PublicTenantDto tenant) => Map(tenant, showBranding: true);

    internal static PublicTenantModel Map(PublicTenantDto tenant, bool showBranding) =>
        new(
            tenant.Slug,
            tenant.Name,
            tenant.SelfRegistrationEnabled,
            tenant.AllowedAuthProviders,
            showBranding ? BrandingJson.ToObject(tenant.BrandingJson) : null);
}
