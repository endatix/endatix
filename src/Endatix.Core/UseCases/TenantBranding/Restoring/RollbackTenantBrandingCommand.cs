using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantBranding.Restoring;

/// <summary>
/// Publishes the current tenant's version before the live one again, as a new version. Two rollbacks in a row toggle
/// between the last two versions.
/// </summary>
/// <remarks>
/// The command checks the <c>branding</c> entitlement. The <c>WhiteLabeling</c> feature flag is the caller's to check,
/// as every flag is checked at the endpoint (<c>EndpointFeatureGate</c>); with the flag off no caller sends this command.
/// </remarks>
/// <param name="ExpectedVersion">The <see cref="Entities.TenantBranding.Version"/> the client read, so a retried rollback
/// is refused instead of undoing itself.</param>
/// <param name="UserId">The user rolling back, recorded on the version; null when not known.</param>
[Experimental("ENDATIX0002")]
public sealed record RollbackTenantBrandingCommand(long ExpectedVersion, long? UserId = null)
    : ICommand<Result<TenantBrandingDto>>;
