using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantBranding.Restoring;

/// <summary>
/// Publishes one of the current tenant's earlier versions again, as a new version.
/// </summary>
/// <remarks>
/// The command checks the <c>branding</c> entitlement. The <c>WhiteLabeling</c> feature flag is the caller's to check,
/// as every flag is checked at the endpoint (<c>EndpointFeatureGate</c>); with the flag off no caller sends this command.
/// </remarks>
/// <param name="Number">The <see cref="Entities.TenantBrandingVersion.Number"/> to publish again.</param>
/// <param name="ExpectedVersion">The <see cref="Entities.TenantBranding.Version"/> the client read.</param>
/// <param name="UserId">The user restoring, recorded on the version; null when not known.</param>
[Experimental("ENDATIX0002")]
public sealed record RestoreTenantBrandingCommand(long Number, long ExpectedVersion, long? UserId = null)
    : ICommand<Result<TenantBrandingDto>>;
