using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Tenants.GetPublicBySlug;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;

namespace Endatix.Api.Endpoints.Public.Tenants;

/// <summary>
/// Unauthenticated tenant discovery by opaque public id. Rate-limited; 404s are not cached.
/// </summary>
public sealed class GetBySlug(IMediator mediator, IMemoryCache cache)
    : Endpoint<GetPublicTenantRequest, Results<Ok<PublicTenantModel>, ProblemHttpResult>>
{
    internal static readonly TimeSpan PublicTenantCacheDuration = TimeSpan.FromMinutes(3);

    public override void Configure()
    {
        Get("tenants/{slug}");
        Group<PublicApiGroup>();
        AllowAnonymous();
        Throttle(20, 60);
        Summary(s =>
        {
            s.Summary = "Get public tenant";
            s.Description = "Returns the tenant name and self-registration policy for an opaque public id. Does not return the numeric tenant id.";
            s.Responses[200] = "Tenant found.";
            s.Responses[404] = "Unknown or deleted public id.";
            s.Responses[429] = "Too many requests.";
        });
    }

    public override async Task<Results<Ok<PublicTenantModel>, ProblemHttpResult>> ExecuteAsync(
        GetPublicTenantRequest request,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"tenant:public:{request.Slug}";
        if (cache.TryGetValue(cacheKey, out PublicTenantModel? cached) && cached is not null)
        {
            return TypedResults.Ok(cached);
        }

        var result = await mediator.Send(new GetPublicTenantQuery(request.Slug), cancellationToken);
        if (result.IsSuccess)
        {
            cache.Set(cacheKey, PublicTenantModel.Map(result.Value), PublicTenantCacheDuration);
        }

        return TypedResultsBuilder
            .MapResult(result, PublicTenantModel.Map)
            .SetTypedResults<Ok<PublicTenantModel>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request for unauthenticated tenant discovery.
/// </summary>
public sealed class GetPublicTenantRequest
{
    /// <summary>
    /// Opaque 8-character YouTube-style public id stored on <c>Tenant.Slug</c>.
    /// </summary>
    public string Slug { get; set; } = string.Empty;
}

/// <summary>
/// Validator for <see cref="GetPublicTenantRequest"/>. Format checks live in the handler so
/// name-like values return 404 rather than 400.
/// </summary>
public sealed class GetPublicTenantValidator : Validator<GetPublicTenantRequest>
{
    public GetPublicTenantValidator()
    {
        RuleFor(request => request.Slug).NotEmpty();
    }
}

/// <summary>
/// Public tenant DTO. Numeric id is intentionally absent.
/// </summary>
public sealed record PublicTenantModel(
    string Slug,
    string Name,
    bool SelfRegistrationEnabled,
    IReadOnlyList<string> AllowedAuthProviders)
{
    public static PublicTenantModel Map(PublicTenantDto tenant) =>
        new(tenant.Slug, tenant.Name, tenant.SelfRegistrationEnabled, tenant.AllowedAuthProviders);
}
