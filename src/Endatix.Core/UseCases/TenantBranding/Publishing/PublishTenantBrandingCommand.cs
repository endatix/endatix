using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantBranding.Publishing;

/// <summary>
/// Publishes a brand override for the current tenant as a new version.
/// </summary>
/// <remarks>
/// The command checks the <c>branding</c> entitlement. The <c>WhiteLabeling</c> feature flag is the caller's to check,
/// as every flag is checked at the endpoint (<c>EndpointFeatureGate</c>); with the flag off no caller sends this command.
/// </remarks>
/// <param name="OverrideJson">The override: logo, icon and accent in the brand pack's shape (see <see cref="Branding.BrandOverride"/>).</param>
/// <param name="ExpectedVersion">The <see cref="Entities.TenantBranding.Version"/> the client read;
/// <see cref="Entities.TenantBranding.UNSAVED_VERSION"/> before the first publish.</param>
/// <param name="UserId">The user publishing, recorded on the version; null when not known.</param>
[Experimental("ENDATIX0002")]
public sealed record PublishTenantBrandingCommand(string OverrideJson, long ExpectedVersion, long? UserId = null)
    : ICommand<Result<TenantBrandingDto>>;
