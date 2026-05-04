using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Authorization.PublicForm;

/// <summary>
/// Issues and validates minimal short-lived ReBAC JWTs for public form access (form id + tenant id).
/// </summary>
public interface IFormAccessTokenService
{
    /// <summary>
    /// Creates a signed JWT with <c>resourceType=form</c>, <c>resourceId</c>, and <c>tid</c>.
    /// </summary>
    Result<FormAccessTokenDto> CreateToken(long formId, long tenantId);

    /// <summary>
    /// Validates the JWT issuer (ReBAC), audience, lifetime, and <c>resourceType</c> / <c>resourceId</c> claims.
    /// </summary>
    Result<FormAccessTokenClaims> ValidateToken(string token);
}
