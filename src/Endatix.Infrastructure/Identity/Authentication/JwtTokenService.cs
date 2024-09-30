using Ardalis.GuardClauses;
using Microsoft.Extensions.Options;
using Endatix.Core.Abstractions;
using Endatix.Core.UseCases.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Endatix.Infrastructure.Identity;
using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Identity.Authentication;

/// <summary>
/// Implements the <see cref="ITokenService" /> interface to manage JWT tokens for users.
/// </summary>
internal sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _jwtOptions;

    /// <summary>
    /// Initializes a new instance of the JwtTokenService class with the specified JWT options.
    /// </summary>
    /// <param name="jwtOptions">The JWT options.</param>
    public JwtTokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;

        // Validate JWT options to ensure they are correctly configured
        Guard.Against.NullOrEmpty(_jwtOptions.SigningKey, nameof(_jwtOptions.SigningKey), "Signing key cannot be empty. Please check your appSettings.");
        Guard.Against.NullOrEmpty(_jwtOptions.Issuer, nameof(_jwtOptions.Issuer), "Issuer cannot be empty. Please check your appSettings");
        Guard.Against.NullOrEmpty(_jwtOptions.Audiences, nameof(_jwtOptions.Audiences), "You need at least one audience in your appSettings.");
        Guard.Against.NegativeOrZero(_jwtOptions.ExpiryInMinutes, nameof(_jwtOptions.ExpiryInMinutes), "Token expiration must be positive number representing minutes for token lifetime");
    }

    /// <inheritdoc />
    public TokenDto IssueToken(User forUser, string? forAudience = null)
    {
        var secret = _jwtOptions.SigningKey;
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        if (string.IsNullOrEmpty(forAudience))
        {
            forAudience = _jwtOptions.Audiences.First();
        }

        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var subject = new ClaimsIdentity(claims: [
                new Claim(JwtRegisteredClaimNames.Sub, forUser.ExternalId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, forUser.Email),
                new Claim(ClaimTypes.NameIdentifier, forUser.ExternalId.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.ADMIN),
                new Claim(ClaimNames.Permission, Allow.AllowAll)
            ]);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = subject,
            Expires = DateTime.Now.AddMinutes(_jwtOptions.ExpiryInMinutes),
            SigningCredentials = credentials,
            Issuer = _jwtOptions.Issuer,
            Audience = forAudience
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);

        return new TokenDto(handler.WriteToken(token), token.ValidTo);
    }

    /// <inheritdoc />
    public Task<Result> RevokeTokensAsync(User forUser, CancellationToken cancellationToken = default) {
        if (forUser == null){
            return Task.FromResult(Result.NotFound());
        }

        // Add removal of Refresh token once feature is implemented.
        // Add Revoking JWT token should we decide to implement this
        return Task.FromResult(Result.Success());
    }
}
