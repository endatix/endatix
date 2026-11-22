using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.TenantBranding;
using Endatix.Core.UseCases.TenantBranding.Publishing;
using FastEndpoints;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Api.Endpoints.TenantBranding;

/// <summary>
/// Publishes a brand override (logo, icon, accent) for the current tenant as a new version.
/// </summary>
[Experimental("ENDATIX0002")]
public sealed class PublishTenantBranding(
    IMediator mediator,
    HybridCache cache,
    ITenantContext tenantContext,
    IUserContext userContext)
    : TenantBrandingWriteEndpoint<PublishTenantBrandingRequest>(mediator, cache, tenantContext, userContext)
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Put("tenant-branding/published");
        ConfigureWrite(
            "Publish the tenant's branding",
            "Publishes the body, a brand override in the brand pack's shape, as a new version and makes it live. " +
            "Publishing the brand that is already live changes nothing.");
    }

    /// <inheritdoc/>
    protected override IRequest<Result<TenantBrandingDto>> Command(
        PublishTenantBrandingRequest request,
        long expectedVersion,
        long? userId) =>
        new PublishTenantBrandingCommand(request.Override.GetRawText(), expectedVersion, userId);
}

/// <summary>Request to publish the tenant's branding.</summary>
[Experimental("ENDATIX0002")]
public sealed class PublishTenantBrandingRequest : ITenantBrandingWriteRequest
{
    /// <inheritdoc/>
    [FromHeader("If-Match", isRequired: false)]
    public string? IfMatch { get; init; }

    /// <summary>The brand override: the whole request body.</summary>
    [FromBody]
    public JsonElement Override { get; init; }
}
