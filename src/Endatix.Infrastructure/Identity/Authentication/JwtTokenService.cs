using Ardalis.GuardClauses;
using Microsoft.Extensions.Options;
using Endatix.Core.Abstractions;
using Endatix.Core.UseCases.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Implements the <see cref="ITokenService" /> interface to manage JWT tokens for users.
/// </summary>
internal sealed class JwtTokenService : ITokenService
{
    private const int JWT_CLOCK_SKEW_IN_SECONDS = 15;

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
        Guard.Against.NegativeOrZero(_jwtOptions.AccessExpiryInMinutes, nameof(_jwtOptions.AccessExpiryInMinutes), "Access Token expiration must be positive number representing minutes for access token lifetime");
        Guard.Against.NegativeOrZero(_jwtOptions.RefreshExpiryInDays, nameof(_jwtOptions.RefreshExpiryInDays), "Refresh Token expiration must be positive number representing days for refresh token lifetime");
    }

    /// <inheritdoc />
    public TokenDto IssueAccessToken(User forUser, string? forAudience = null)
    {
        var secret = _jwtOptions.SigningKey;
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        if (string.IsNullOrEmpty(forAudience))
        {
            forAudience = _jwtOptions.Audiences.First();
        }

        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var subject = new ClaimsIdentity(claims: [
                new Claim(JwtRegisteredClaimNames.Sub, forUser.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, forUser.Email.ToString()),
                new Claim(ClaimTypes.NameIdentifier, forUser.UserName.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.ADMIN),
                new Claim(ClaimNames.Permission, Allow.AllowAll)
            ]);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = subject,
            Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessExpiryInMinutes),
            SigningCredentials = credentials,
            Issuer = _jwtOptions.Issuer,
            Audience = forAudience
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);

        return new TokenDto(handler.WriteToken(token), token.ValidTo);
    }

    public async Task<Result<long>> ValidateAccessTokenAsync(string accessToken, bool validateLifetime = true)
    {
        var validationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)),
            ValidIssuer = _jwtOptions.Issuer,
            ValidAudiences = _jwtOptions.Audiences,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = validateLifetime,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(JWT_CLOCK_SKEW_IN_SECONDS)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var result = await tokenHandler.ValidateTokenAsync(accessToken, validationParameters);
        if (!result.IsValid)
        {
            return Result.Invalid(new ValidationError(result.Exception?.Message ?? "Invalid access token"));
        }

        var validatedJwtToken = (JwtSecurityToken)result.SecurityToken;
        if (!long.TryParse(validatedJwtToken.Subject, out var userId))
        {
            return Result.Invalid(new ValidationError("Invalid user ID"));
        }

        return Result.Success(userId);
    }

    public TokenDto IssueRefreshToken()
    {
        var token = Guid.NewGuid().ToString("N");
        var expireAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshExpiryInDays);

        return new TokenDto(token, expireAt);
    }

    /// <inheritdoc />
    public Task<Result> RevokeTokensAsync(User forUser, CancellationToken cancellationToken = default)
    {
        if (forUser == null)
        {
            return Task.FromResult(Result.NotFound());
        }

        // Add removal of Refresh token once feature is implemented.
        // Add Revoking JWT token should we decide to implement this
        return Task.FromResult(Result.Success());
    }
}
