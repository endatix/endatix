
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

namespace Endatix.Identity.Authentication;

internal sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _jwtOptions;

    public JwtTokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;

        Guard.Against.NullOrEmpty(_jwtOptions.SigningKey, nameof(_jwtOptions.SigningKey), "Signing key cannot be empty. Please check your appSettings.");
        Guard.Against.NullOrEmpty(_jwtOptions.Issuer, nameof(_jwtOptions.Issuer), "Issuer cannot be empty. Please check your appSettings");
        Guard.Against.NullOrEmpty(_jwtOptions.Audiences, nameof(_jwtOptions.Audiences), "You need at least one audience in your appSettings.");
        Guard.Against.NegativeOrZero(_jwtOptions.ExpiryInMinutes, nameof(_jwtOptions.ExpiryInMinutes), "Token expiration must be positive number representing minutes for token lifetime");
    }

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
                new Claim(JwtRegisteredClaimNames.Sub, forUser.Email),
                new Claim(JwtRegisteredClaimNames.Email, forUser.Email),
                new Claim(ClaimTypes.Role, RoleNames.ADMIN),
                new Claim(ClaimNames.Permission, Allow.AllowAll),
                new Claim(ClaimNames.EmailVerified, forUser.IsVerified.ToString())
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
}
