using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantBranding.Resetting;

/// <summary>
/// Turns off the current tenant's custom brand, so its pages show the deployment brand again. The reset is a new
/// version in the history; restoring an earlier version brings that brand back.
/// </summary>
/// <remarks>
/// The command checks the <c>branding</c> entitlement. The <c>WhiteLabeling</c> feature flag is the caller's to check,
/// as every flag is checked at the endpoint (<c>EndpointFeatureGate</c>); with the flag off no caller sends this command.
/// </remarks>
/// <param name="ExpectedVersion">The <see cref="Entities.TenantBranding.Version"/> the client read.</param>
/// <param name="UserId">The user resetting, recorded on the version; null when not known.</param>
[Experimental("ENDATIX0002")]
public sealed record ResetTenantBrandingCommand(long ExpectedVersion, long? UserId = null)
    : ICommand<Result<TenantBrandingDto>>;
