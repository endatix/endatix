using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantBranding.Rollback;

/// <summary>
/// Returns the current tenant to its previous brand override.
/// </summary>
/// <remarks>
/// The command checks the <c>branding</c> entitlement. The <c>WhiteLabeling</c> feature flag is the caller's to check,
/// as every flag is checked at the endpoint (<c>EndpointFeatureGate</c>); with the flag off no caller sends this command.
/// </remarks>
/// <param name="ExpectedRevision">The revision the client read, so a retried rollback is refused instead of undoing itself.</param>
public sealed record RollbackTenantBrandingCommand(long ExpectedRevision) : ICommand<Result<TenantBrandingDto>>;
