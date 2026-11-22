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
/// Publishes the current tenant's branding version before the live one again: a restore of the predecessor.
/// </summary>
[Experimental("ENDATIX0002")]
public sealed class RollbackTenantBranding(
    IMediator mediator,
    HybridCache cache,
    ITenantContext tenantContext,
    IUserContext userContext)
    : TenantBrandingWriteEndpoint<RollbackTenantBrandingRequest>(mediator, cache, tenantContext, userContext)
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Post("tenant-branding/rollback");
        ConfigureWrite(
            "Roll back the tenant's branding",
            "Restores the version before the live one, as a new version; two rollbacks toggle between the last two. " +
            "With nothing before the live version it is 400 with the code no_previous_version.");
    }

    /// <inheritdoc/>
    protected override IRequest<Result<TenantBrandingDto>> Command(
        RollbackTenantBrandingRequest request,
        long expectedVersion,
        long? userId) =>
        new RollbackTenantBrandingCommand(expectedVersion, userId);
}

/// <summary>Request to roll back the tenant's branding.</summary>
[Experimental("ENDATIX0002")]
public sealed class RollbackTenantBrandingRequest : ITenantBrandingWriteRequest
{
    /// <inheritdoc/>
    [FromHeader("If-Match", isRequired: false)]
    public string? IfMatch { get; init; }
}
