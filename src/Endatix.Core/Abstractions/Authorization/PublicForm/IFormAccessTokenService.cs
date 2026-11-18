using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Authorization.PublicForm;

/// <summary>
/// Issues and validates minimal short-lived ReBAC JWTs for public form access (form id + tenant id).
/// </summary>
public interface IFormAccessTokenService
{
    /// <summary>
    /// Creates a signed JWT for public form access.
    /// </summary>
    /// <param name="formId">The ID of the form to create the access token for.</param>
    /// <param name="tenantId">The ID of the tenant to create the access token for.</param>
    /// <param name="isPublicGrant">
    /// <c>true</c> when the token is issued because the form is public; it is then refused once the form is not.
    /// </param>
    /// <returns>The access token data.</returns>
    Result<FormAccessTokenDto> CreateToken(long formId, long tenantId, bool isPublicGrant = true);

    /// <summary>
    /// Validates a signed JWT for public form access.   
    /// </summary>
    /// <param name="token">The token to validate.</param>
    /// <returns>The access token claims.</returns>
    Result<FormAccessTokenClaims> ValidateToken(string token);
}
