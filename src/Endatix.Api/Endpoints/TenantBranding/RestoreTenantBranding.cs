using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.TenantBranding;
using Endatix.Core.UseCases.TenantBranding.Restoring;
using FastEndpoints;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Api.Endpoints.TenantBranding;

/// <summary>
/// Publishes one of the current tenant's earlier branding versions again, as a new version.
/// </summary>
[Experimental("ENDATIX0002")]
public sealed class RestoreTenantBranding(
    IMediator mediator,
    HybridCache cache,
    ITenantContext tenantContext,
    IUserContext userContext)
    : TenantBrandingWriteEndpoint<RestoreTenantBrandingRequest>(mediator, cache, tenantContext, userContext)
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Post("tenant-branding/restore");
        ConfigureWrite(
            "Restore a branding version",
            "Publishes version number again as a new version (source restore). History is never rewound. " +
            "An unknown number is 400 with the code unknown_version.");
    }

    /// <inheritdoc/>
    protected override IRequest<Result<TenantBrandingDto>> Command(
        RestoreTenantBrandingRequest request,
        long expectedVersion,
        long? userId) =>
        new RestoreTenantBrandingCommand(request.Number, expectedVersion, userId);
}

/// <summary>Request to restore a branding version.</summary>
[Experimental("ENDATIX0002")]
public sealed class RestoreTenantBrandingRequest : ITenantBrandingWriteRequest
{
    /// <inheritdoc/>
    [FromHeader("If-Match", isRequired: false)]
    public string? IfMatch { get; init; }

    /// <summary>The number of the version to publish again, from the history list.</summary>
    public long Number { get; init; }
}
