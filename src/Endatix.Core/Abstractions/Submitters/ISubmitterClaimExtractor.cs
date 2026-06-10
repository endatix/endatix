using System.Security.Claims;

namespace Endatix.Core.Abstractions.Submitters;

/// <summary>
/// Interface for extracting submitter claims from a claims principal.
/// </summary>
public interface ISubmitterClaimExtractor
{
    /// <summary>
    /// Extractor precedence. Lower values run first, allowing custom extractors to override built-ins.
    /// </summary>
    int Priority => 100;

    /// <summary>
    /// Whether the submitter claim extractor can extract claims from the principal.
    /// </summary>
    /// <param name="principal">The principal to extract claims from.</param>
    /// <returns>True if the submitter claim extractor can extract claims from the principal, false otherwise.</returns>
    bool CanExtract(ClaimsPrincipal principal);

    /// <summary>
    /// Extracts the submitter claims from the principal.
    /// </summary>
    /// <param name="principal">The principal to extract claims from.</param>
    /// <returns>The submitter claims.</returns>
    SubmitterExtractionInput Extract(ClaimsPrincipal principal);
}
