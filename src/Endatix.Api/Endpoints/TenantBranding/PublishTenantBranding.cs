using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
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
/// a repeated key) with its problem codes (<c>required</c>, <c>invalid_json</c>, <c>too_large</c>).
/// <see cref="BoundedBrandOverrideBinder"/> reads at most one character past the parser's bound, so a larger body,
/// chunked or over the server's own limit, is <c>too_large</c> without being read whole. A request size limit is not
/// used: the server refuses the body while it is bound, which the exception handler answers with a 500.
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
    protected override void ConfigureBody()
    {
        Description(builder => builder.Accepts<BrandOverrideBody>("application/json"));
        RequestBinder(new BoundedBrandOverrideBinder());
    }

    /// <inheritdoc/>
    protected override IRequest<Result<TenantBrandingDto>> Command(
        PublishTenantBrandingRequest request,
        long expectedVersion,
        long? userId) =>
        new PublishTenantBrandingCommand(request.Content, expectedVersion, userId);
}

/// <summary>Request to publish the tenant's branding: the raw body, read by <see cref="BoundedBrandOverrideBinder"/>.</summary>
[Experimental("ENDATIX0002")]
public sealed class PublishTenantBrandingRequest
{
    /// <summary>
    /// The brand override as sent, before it is parsed; cut one character past the parser's bound when longer.
    /// </summary>
    public string Content { get; set; } = "";
}

/// <summary>A brand override in the brand pack's shape, version 1: the logo, the icon and the accent, each optional.</summary>
/// <remarks>
/// Only the OpenAPI document uses this type: the body is read as text and checked by the brand override's own parser.
/// FastEndpoints describes a body by a type with public properties, so <c>Accepts&lt;object&gt;</c> fails the whole
/// document (endatix/endatix#1841). <c>OpenApiDocumentFlowTests</c> ties these names to the parser's root fields.
/// </remarks>
internal sealed class BrandOverrideBody
{
    /// <summary>The brand pack version the override follows. Required; only 1 is supported.</summary>
    [Required]
    public int Version { get; init; } = 1;

    /// <summary>The logo (<c>light</c>, optional <c>dark</c>) and the icon, as storage keys under the tenant's brand folder.</summary>
    public BrandOverrideAssets Assets { get; init; } = new();

    /// <summary>The accent: <c>color.primary</c> and optional <c>color.primary-on-dark</c>, each <c>{"$type": "color", "$value"}</c>.</summary>
    public BrandOverrideTokens Tokens { get; init; } = new();
}

/// <summary>The override's <c>assets</c>: an object, optional. Its fields are documented on <see cref="BrandOverrideBody.Assets"/>.</summary>
/// <remarks>
/// A type, not <c>object?</c>, so the document shows an object that is not nullable (the parser refuses a null
/// <c>assets</c>). The extension data lets the document allow the nested fields, which the parser checks.
/// </remarks>
internal sealed class BrandOverrideAssets
{
    /// <summary>The nested fields, as the brand override's parser reads them.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Fields { get; init; } = [];
}

/// <summary>The override's <c>tokens</c>: an object, optional. Its fields are documented on <see cref="BrandOverrideBody.Tokens"/>.</summary>
/// <remarks>
/// A type, not <c>object?</c>, so the document shows an object that is not nullable (the parser refuses a null
/// <c>tokens</c>). The extension data lets the document allow the nested fields, which the parser checks.
/// </remarks>
internal sealed class BrandOverrideTokens
{
    /// <summary>The nested fields, as the brand override's parser reads them.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Fields { get; init; } = [];
}
