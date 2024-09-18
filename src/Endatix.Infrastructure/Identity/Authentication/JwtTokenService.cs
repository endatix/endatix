
using Ardalis.GuardClauses;
using Microsoft.Extensions.Options;
using Endatix.Core.Abstractions;
using Endatix.Core.UseCases.Security;
using Endatix.Infrastructure.Auth;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Endatix.Identity.Authentication;

internal sealed class JwtTokenService : ITokenService
{
    private readonly SecuritySettings _settings;

    public JwtTokenService(IOptions<SecuritySettings> securityOptions)
    {
        _settings = securityOptions.Value;

        Guard.Against.NullOrEmpty(_settings.JwtSigningKey, nameof(_settings.JwtSigningKey), "Signing key cannot be empty");
        Guard.Against.NegativeOrZero(_settings.JwtExpiryInMinutes, nameof(_settings.JwtExpiryInMinutes), "Token expiration must be positive number representing minutes for token lifetime");
    }

    public TokenDto IssueToken(UserDto forUser)
    {
        var secret = _settings.JwtSigningKey;
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, forUser.Email),
                new Claim(JwtRegisteredClaimNames.Email, forUser.Email),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim(ClaimTypes.Role, "Manager"),
                new Claim("permission", "give.all"),
                new Claim("permission", "forms.read"),
                new Claim("email_verified", "true")
            ]),
            Expires = DateTime.Now.AddMinutes(30),
            SigningCredentials = credentials,
            Issuer = "configuration['JwtSettings:Issuer']",
            Audience = "configuration['JwtSettings:Audience']"
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);

        return new TokenDto(handler.WriteToken(token), token.ValidTo);
    }
}
