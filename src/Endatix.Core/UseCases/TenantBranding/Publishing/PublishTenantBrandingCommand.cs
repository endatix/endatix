using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantBranding.Publishing;

/// <summary>
/// Publishes a brand override for the current tenant.
/// </summary>
/// <remarks>
/// The command checks the <c>branding</c> entitlement. The <c>WhiteLabeling</c> feature flag is the caller's to check,
/// as every flag is checked at the endpoint (<c>EndpointFeatureGate</c>); with the flag off no caller sends this command.
/// </remarks>
/// <param name="OverrideJson">The override: logo, icon and accent in the brand pack's shape (see <see cref="Branding.BrandOverride"/>).</param>
/// <param name="ExpectedRevision">The revision the client read; 0 before the first publish.</param>
public sealed record PublishTenantBrandingCommand(string OverrideJson, long ExpectedRevision) : ICommand<Result<TenantBrandingDto>>;
