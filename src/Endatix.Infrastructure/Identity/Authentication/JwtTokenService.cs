using Ardalis.GuardClauses;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Endatix.Core.Abstractions;
using Endatix.Core.UseCases.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;
using Endatix.Infrastructure.Identity.Authentication.Providers;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Implements the <see cref="IUserTokenService" /> interface to manage JWT tokens for users.
/// </summary>
internal sealed class JwtTokenService : IUserTokenService
{
    private const int JWT_CLOCK_SKEW_IN_SECONDS = 15;

    private readonly EndatixJwtOptions _endatixJwtOptions;
    private readonly IAuthSchemeSelector _authSchemeSelector;
    private readonly ILogger<JwtTokenService> _logger;

    /// <summary>
    /// Initializes a new instance of the JwtTokenService class with the specified JWT options.
    /// </summary>
    /// <param name="endatixJwtOptions">The EndatixJwt options.</param>
    /// <param name="configuration">The configuration to check for JwtOptions presence.</param>
    /// <param name="logger"></param>
    /// <param name="authSchemeSelector"></param>
    public JwtTokenService(IOptions<EndatixJwtOptions> endatixJwtOptions, IConfiguration configuration, ILogger<JwtTokenService> logger, IAuthSchemeSelector authSchemeSelector)
    {
        _endatixJwtOptions = endatixJwtOptions.Value;
        _authSchemeSelector = authSchemeSelector;
        _logger = logger;

        // Check if JwtOptions section actually exists in configuration and migrate if needed
        var jwtOptionsSection = configuration.GetSection(JwtOptions.SECTION_NAME);
        if (jwtOptionsSection.Exists() && jwtOptionsSection.Get<JwtOptions>() is JwtOptions legacyJwtOptions)
        {
            logger.LogWarning("JwtOptions are depreciated. Move the configuration to Endatix:Auth:EndatixJwt. Applying mapping of old values to EndatixJwtOptions.");
            _endatixJwtOptions = JwtOptionsMapper.Map(legacyJwtOptions, _endatixJwtOptions);
        }

        // Validate EndatixJwt Options options to ensure they are correctly configured
        Guard.Against.NullOrEmpty(_endatixJwtOptions.SigningKey, nameof(_endatixJwtOptions.SigningKey), "Signing key cannot be empty. Please check your appSettings.");
        Guard.Against.NullOrEmpty(_endatixJwtOptions.Issuer, nameof(_endatixJwtOptions.Issuer), "Issuer cannot be empty. Please check your appSettings");
        Guard.Against.NullOrEmpty(_endatixJwtOptions.Audiences, nameof(_endatixJwtOptions.Audiences), "You need at least one audience in your appSettings.");
        Guard.Against.NegativeOrZero(_endatixJwtOptions.AccessExpiryInMinutes, nameof(_endatixJwtOptions.AccessExpiryInMinutes), "Access Token expiration must be positive number representing minutes for access token lifetime");
        Guard.Against.NegativeOrZero(_endatixJwtOptions.RefreshExpiryInDays, nameof(_endatixJwtOptions.RefreshExpiryInDays), "Refresh Token expiration must be positive number representing days for refresh token lifetime");
    }

    /// <inheritdoc />
    public TokenDto IssueAccessToken(User forUser, string? forAudience = null)
    {
        var secret = _endatixJwtOptions.SigningKey;
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        if (string.IsNullOrEmpty(forAudience))
        {
            forAudience = _endatixJwtOptions.Audiences.First();
        }

        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var subject = new ClaimsIdentity(claims: [
                new Claim(JwtRegisteredClaimNames.Sub, forUser.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, forUser.Email.ToString()),
                new Claim(JwtRegisteredClaimNames.EmailVerified, forUser.IsVerified? "true" : "false"),
                new Claim(ClaimNames.TenantId, forUser.TenantId.ToString())
            ]);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = subject,
            Expires = DateTime.UtcNow.AddMinutes(_endatixJwtOptions.AccessExpiryInMinutes),
            SigningCredentials = credentials,
            Issuer = _endatixJwtOptions.Issuer,
            Audience = forAudience
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);

        return new TokenDto(handler.WriteToken(token), token.ValidTo);
    }

    public async Task<Result<long>> ValidateAccessTokenAsync(string accessToken, bool validateLifetime = true)
    {
        var selectedScheme = _authSchemeSelector.SelectScheme(accessToken);
        if (selectedScheme != AuthSchemes.EndatixJwt)
        {
            _logger.LogWarning("Attempted to validate access token with scheme: {selectedScheme}. Only Endatix JWT tokens are supported.", selectedScheme);
            return Result.Invalid(new ValidationError($"Token validation not supported for scheme: {selectedScheme}."));
        }

        var validationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_endatixJwtOptions.SigningKey)),
            ValidIssuer = _endatixJwtOptions.Issuer,
            ValidAudiences = _endatixJwtOptions.Audiences,
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
        var expireAt = DateTime.UtcNow.AddDays(_endatixJwtOptions.RefreshExpiryInDays);

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
