using System.Security.Claims;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.Submitters;

/// <summary>
/// Extracts submitter claims from an Endatix JWT.
/// </summary>
internal sealed class EndatixSubmitterClaimExtractor(
    AuthProviderRegistry authProviderRegistry,
    IOptions<SubmitterOptions> options,
    SubmitterClaimReader claimReader)
    : ISubmitterClaimExtractor
{
    private readonly SubmitterOptions _options = options.Value;

    /// <inheritdoc />
    public int Priority => 100;

    /// <inheritdoc />
    public bool CanExtract(ClaimsPrincipal principal)
    {
        if (!string.Equals(
            authProviderRegistry.ResolveAuthProviderFromIssuer(principal.GetIssuer()),
            AuthProviders.Endatix,
            StringComparison.Ordinal))
        {
            return false;
        }

        var subject = claimReader.ResolveNativeTokenSubject(principal);
        return principal.Identity?.IsAuthenticated == true &&
            !string.IsNullOrWhiteSpace(subject) &&
            long.TryParse(subject, out _);
    }

    /// <inheritdoc />
    public SubmitterExtractionInput Extract(ClaimsPrincipal principal)
    {
        var subject = claimReader.ResolveNativeTokenSubject(principal);
        if (!long.TryParse(subject, out var appUserId))
        {
            throw new InvalidOperationException($"Failed to parse Endatix subject '{subject}' as long. CanExtract should have prevented this.");
        }

        return new SubmitterExtractionInput(
            AuthProviders.Endatix,
            null,
            appUserId.ToString(),
            appUserId,
            claimReader.ResolveProfile(principal, _options));
    }
}
