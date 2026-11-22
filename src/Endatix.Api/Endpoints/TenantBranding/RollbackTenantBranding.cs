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
/// Publishes the current tenant's branding version before the live one again: a restore of the predecessor.
/// </summary>
[Experimental("ENDATIX0002")]
public sealed class RollbackTenantBranding(
    IMediator mediator,
    IPublicTenantCacheInvalidator publicTenantCache,
    IUserContext userContext)
    : TenantBrandingWriteEndpoint<EmptyRequest>(mediator, publicTenantCache, userContext)
{
    /// <inheritdoc/>
    protected override WriteRoute Route { get; } = new(
        Http.POST,
        "tenant-branding/rollback",
        "Roll back the tenant's branding",
        "Restores the version before the live one as a new version, with no body; two rollbacks toggle between the " +
        "last two. With nothing before the live version it is 400 with the code no_previous_version.");

    /// <inheritdoc/>
    protected override IRequest<Result<TenantBrandingDto>> Command(EmptyRequest request, long expectedVersion, long? userId) =>
        new RollbackTenantBrandingCommand(expectedVersion, userId);
}
