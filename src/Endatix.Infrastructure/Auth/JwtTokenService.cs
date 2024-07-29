using System;
using System.Linq;
using Ardalis.GuardClauses;
using FastEndpoints.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Endatix.Core.Abstractions;
using Endatix.Core.UseCases.Security;
using Endatix.Infrastructure.Auth;

namespace Endatix.Infrastructure;

public class JwtTokenService : ITokenService
{
    private readonly SecuritySettings _settings;

    public JwtTokenService(IOptions<SecuritySettings> _securityOptions)
    {
        _settings = _securityOptions.Value;

        Guard.Against.NullOrEmpty(_settings.JwtSigningKey, nameof(_settings.JwtSigningKey), "Signing key cannot be empty");
        Guard.Against.NegativeOrZero(_settings.JwtExpiryInMinutes, nameof(_settings.JwtExpiryInMinutes), "Token expiration must be positive number representing minutes for token lifetime");
    }

    public TokenDto IssueToken(UserDto forUser)
    {
        var signingKey = _settings.JwtSigningKey;
        var expireAt = DateTime.UtcNow.AddMinutes(_settings.JwtExpiryInMinutes);
        var rolesToAdd = forUser.Roles.Any() ? forUser.Roles : [SecurityConstants.DefaultRoles.ADMIN_ROLE];

        var jwtToken = JwtBearer.CreateToken(
            o =>
            {
                o.SigningKey = signingKey;
                o.ExpireAt = expireAt;
                o.User.Roles.Add(rolesToAdd);
                o.User.Claims.Add(("UserName", forUser.Email));
                o.User["UserId"] = "001"; //indexer based claim setting
            });

        return new TokenDto(jwtToken, expireAt);
    }
}
