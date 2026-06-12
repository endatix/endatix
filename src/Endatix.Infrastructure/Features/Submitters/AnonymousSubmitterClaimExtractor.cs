using System.Security.Claims;
using Endatix.Core.Abstractions.Submitters;

namespace Endatix.Infrastructure.Features.Submitters;

/// <summary>
/// Extracts submitter claims from an anonymous principal.
/// </summary>
internal sealed class AnonymousSubmitterClaimExtractor : ISubmitterClaimExtractor
{
    /// <inheritdoc />
    public int Priority => 300;

    /// <inheritdoc />

    public bool CanExtract(ClaimsPrincipal principal) => principal.Identity?.IsAuthenticated != true;

    /// <inheritdoc />
    public SubmitterExtractionInput Extract(ClaimsPrincipal principal) =>
        new(
            SubmitterAuthProviders.Anonymous,
            ExternalSubjectId: null,
            DisplayId: null,
            AppUserId: null);
}
