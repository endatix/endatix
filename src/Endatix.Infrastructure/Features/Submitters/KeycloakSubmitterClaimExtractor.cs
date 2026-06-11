using System.Security.Claims;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.Submitters;

/// <summary>
/// Extracts submitter claims from a Keycloak principal.
/// </summary>
internal sealed class KeycloakSubmitterClaimExtractor(
    AuthProviderRegistry authProviderRegistry,
    IOptions<SubmitterOptions> options,
    SubmitterClaimReader claimReader)
    : ISubmitterClaimExtractor
{
    private readonly SubmitterOptions _options = options.Value;

    /// <inheritdoc />  
    public int Priority => 150;

    /// <inheritdoc />
    public bool CanExtract(ClaimsPrincipal principal)
    {
        if (!string.Equals(
            authProviderRegistry.ResolveAuthProviderFromIssuer(principal.GetIssuer()),
            AuthProviders.Keycloak,
            StringComparison.Ordinal))
        {
            return false;
        }

        var subject = claimReader.ResolveExternalSubject(principal);
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(subject) || long.TryParse(subject, out _))
        {
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public SubmitterExtractionInput Extract(ClaimsPrincipal principal)
    {
        return new SubmitterExtractionInput(
            AuthProviders.Keycloak,
            claimReader.ResolveExternalSubject(principal),
            claimReader.ResolveDisplayId(principal, _options),
            null,
            claimReader.ResolveProfile(principal, _options));
    }
}
