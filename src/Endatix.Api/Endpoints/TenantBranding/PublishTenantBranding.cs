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
using Microsoft.AspNetCore.Mvc;

namespace Endatix.Api.Endpoints.TenantBranding;

/// <summary>
/// Publishes a brand override (logo, icon, accent) for the current tenant as a new version.
/// </summary>
/// <remarks>
/// The body is read as text, not bound as JSON, so the brand override's own parser answers every body (empty, not JSON,
/// a repeated key) with its problem codes (<c>required</c>, <c>invalid_json</c>, <c>too_large</c>).
/// </remarks>
[Experimental("ENDATIX0002")]
public sealed class PublishTenantBranding(
    IMediator mediator,
    IPublicTenantCacheInvalidator publicTenantCache,
    IUserContext userContext)
    : TenantBrandingWriteEndpoint<PublishTenantBrandingRequest>(mediator, publicTenantCache, userContext)
{
    // The brand override refuses more than 4,096 characters (too_large); a UTF-8 character is at most 4 bytes, so a
    // body this size still gets that answer, and a larger one is 413 (RequestBodyTooLargeFilter).
    private const long MAX_BODY_BYTES = 16 * 1024 + 4;

    /// <inheritdoc/>
    protected override WriteRoute Route { get; } = new(
        Http.PUT,
        "tenant-branding/published",
        "Publish the tenant's branding",
        "Publishes the body, a brand override in the brand pack's shape (application/json), as a new version and makes " +
        "it live. Publishing the brand that is already live changes nothing.");

    /// <inheritdoc/>
    protected override void ConfigureBody()
    {
        Description(builder => builder
            .Accepts<object>("application/json")
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .WithMetadata(new RequestSizeLimitAttribute(MAX_BODY_BYTES)));
        Options(builder => builder.AddEndpointFilter<RequestBodyTooLargeFilter>());
    }

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
