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
    IOptions<SubmitterOptions> options)
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

        var subject = SubmitterClaimReader.ResolveExternalSubject(principal);
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
            SubmitterClaimReader.ResolveExternalSubject(principal),
            SubmitterClaimReader.ResolveDisplayId(principal, _options),
            null,
            SubmitterClaimReader.ResolveProfile(principal, _options));
    }
}
