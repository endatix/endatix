using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantBranding.Get;

/// <summary>
/// Reads the current tenant's branding: whether the tenant may brand, and the live version.
/// </summary>
/// <remarks>The <c>WhiteLabeling</c> feature flag is the caller's to check (<c>EndpointFeatureGate</c>).</remarks>
[Experimental("ENDATIX0002")]
public sealed record GetTenantBrandingQuery : IQuery<Result<TenantBrandingReadDto>>;

/// <summary>A tenant's branding as an admin reads it.</summary>
/// <param name="Entitled">Whether the tenant's plan includes branding. When false, nothing is published.</param>
/// <param name="Branding">The live version, and the <see cref="TenantBrandingDto.Version"/> the next write names.</param>
[Experimental("ENDATIX0002")]
public sealed record TenantBrandingReadDto(bool Entitled, TenantBrandingDto Branding);
