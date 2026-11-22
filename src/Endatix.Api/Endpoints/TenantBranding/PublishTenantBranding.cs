using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.TenantBranding;
using Endatix.Core.UseCases.TenantBranding.Publishing;
using Endatix.Infrastructure.Caching;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Endpoints.TenantBranding;

/// <summary>
/// Publishes a brand override (logo, icon, accent) for the current tenant as a new version.
/// </summary>
/// <remarks>
/// The body is read as text, not bound as JSON, so the brand override's own parser answers every body (empty, not JSON,
/// a repeated key) with its problem codes (<c>required</c>, <c>invalid_json</c>, <c>too_large</c>). The body has no
/// limit of its own below the server's: a request size limit answers 500 here, because the server refuses the body
/// while FastEndpoints binds it, so the parser's <c>too_large</c> is the bound.
/// </remarks>
[Experimental("ENDATIX0002")]
public sealed class PublishTenantBranding(
    IMediator mediator,
    IPublicTenantCacheInvalidator publicTenantCache,
    IUserContext userContext)
    : TenantBrandingWriteEndpoint<PublishTenantBrandingRequest>(mediator, publicTenantCache, userContext)
{
    /// <inheritdoc/>
    protected override WriteRoute Route { get; } = new(
        Http.PUT,
        "tenant-branding/published",
        "Publish the tenant's branding",
        "Publishes the body, a brand override in the brand pack's shape (application/json), as a new version and makes " +
        "it live. Publishing the brand that is already live changes nothing.");

    /// <inheritdoc/>
    protected override void ConfigureBody() =>
        Description(builder => builder.Accepts<object>("application/json"));

    /// <inheritdoc/>
    protected override IRequest<Result<TenantBrandingDto>> Command(
        PublishTenantBrandingRequest request,
        long expectedVersion,
        long? userId) =>
        new PublishTenantBrandingCommand(request.Content, expectedVersion, userId);
}

/// <summary>Request to publish the tenant's branding: the raw body.</summary>
[Experimental("ENDATIX0002")]
public sealed class PublishTenantBrandingRequest : IPlainTextRequest
{
    /// <summary>The brand override as sent, before it is parsed.</summary>
    public string Content { get; set; } = "";
}
