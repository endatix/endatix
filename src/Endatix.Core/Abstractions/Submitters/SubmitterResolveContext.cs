using System.Security.Claims;

namespace Endatix.Core.Abstractions.Submitters;

/// <summary>
/// Context for resolving a submitter from a claims principal.
/// </summary>
/// <param name="TenantId">The tenant ID.</param>
/// <param name="Principal">The claims principal to resolve the submitter from.</param>
/// <param name="Submitter">The submitter input to resolve the submitter from.</param>
public sealed record SubmitterResolveContext(
    long TenantId,
    ClaimsPrincipal? Principal = null,
    SubmitterInput? Submitter = null);
