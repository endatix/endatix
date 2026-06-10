using System.Security.Claims;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Infrastructure.Identity.Authentication;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.Submitters;

/// <summary>
/// Extracts submitter claims from an Endatix JWT.
/// </summary>
internal sealed class EndatixSubmitterClaimExtractor(
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
        var subject = claimReader.ResolveEndatixSubject(principal);
        return principal.Identity?.IsAuthenticated == true &&
            !string.IsNullOrWhiteSpace(subject) &&
            long.TryParse(subject, out _);
    }

    /// <inheritdoc />
    public SubmitterExtractionInput Extract(ClaimsPrincipal principal)
    {
        var subject = claimReader.ResolveEndatixSubject(principal);
        _ = long.TryParse(subject, out var appUserId);

        return new SubmitterExtractionInput(
            AuthProviders.Endatix,
            null,
            claimReader.ResolvePreferredUserName(principal),
            appUserId,
            claimReader.ResolveProfile(principal, _options));
    }
}
