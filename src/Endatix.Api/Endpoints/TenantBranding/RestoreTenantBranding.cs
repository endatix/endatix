using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.TenantBranding;
using Endatix.Core.UseCases.TenantBranding.Restoring;
using Endatix.Infrastructure.Caching;
using FastEndpoints;
using MediatR;

namespace Endatix.Api.Endpoints.TenantBranding;

/// <summary>
/// Publishes one of the current tenant's earlier branding versions again, as a new version.
/// </summary>
[Experimental("ENDATIX0002")]
public sealed class RestoreTenantBranding(
    IMediator mediator,
    IPublicTenantCacheInvalidator publicTenantCache,
    IUserContext userContext)
    : TenantBrandingWriteEndpoint<RestoreTenantBrandingRequest>(mediator, publicTenantCache, userContext)
{
    /// <inheritdoc/>
    protected override WriteRoute Route { get; } = new(
        Http.POST,
        "tenant-branding/restore",
        "Restore a branding version",
        "Publishes version number again as a new version (source restore); history is never rewound. A number the " +
        "tenant does not have is 400 with the code unknown_version, checked before If-Match.");

    /// <inheritdoc/>
    protected override IRequest<Result<TenantBrandingDto>> Command(
        RestoreTenantBrandingRequest request,
        long expectedVersion,
        long? userId) =>
        new RestoreTenantBrandingCommand(request.Number, expectedVersion, userId);
}

/// <summary>Request to restore a branding version.</summary>
[Experimental("ENDATIX0002")]
public sealed class RestoreTenantBrandingRequest
{
    /// <summary>The number of the version to publish again, from the history list.</summary>
    public long Number { get; init; }
}
