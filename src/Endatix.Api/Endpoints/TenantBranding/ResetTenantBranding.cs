using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.TenantBranding;
using Endatix.Core.UseCases.TenantBranding.Resetting;
using Endatix.Infrastructure.Caching;
using FastEndpoints;
using MediatR;

namespace Endatix.Api.Endpoints.TenantBranding;

/// <summary>
/// Turns off the current tenant's custom brand, so its pages show the deployment brand again.
/// </summary>
[Experimental("ENDATIX0002")]
public sealed class ResetTenantBranding(
    IMediator mediator,
    IPublicTenantCacheInvalidator publicTenantCache,
    IUserContext userContext)
    : TenantBrandingWriteEndpoint<EmptyRequest>(mediator, publicTenantCache, userContext)
{
    /// <inheritdoc/>
    protected override WriteRoute Route { get; } = new(
        Http.POST,
        "tenant-branding/reset",
        "Reset the tenant's branding to the deployment brand",
        "Shows the deployment brand again, with no body: published is null afterwards, and the reset is a new version " +
        "in the history (source reset), so restoring an earlier version brings that brand back. When no custom brand " +
        "is live it changes nothing and adds no version.");

    /// <inheritdoc/>
    protected override IRequest<Result<TenantBrandingDto>> Command(EmptyRequest request, long expectedVersion, long? userId) =>
        new ResetTenantBrandingCommand(expectedVersion, userId);
}
