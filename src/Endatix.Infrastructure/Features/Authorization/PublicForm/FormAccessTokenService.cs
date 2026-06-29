using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization.PublicForm;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Endatix.Infrastructure.Features.Authorization.PublicForm;

/// <summary>
/// Issues and validates minimal HS256 ReBAC JWTs for public form surface (form id + tenant id).
/// Payload: <c>iss</c> (ReBAC issuer), <c>aud</c>, <c>exp</c>, <c>resourceType=form</c>, <c>resourceId</c>, <c>tid</c>.
/// </summary>
/// <remarks>
/// Registered as scoped with <see cref="IOptionsSnapshot{TOptions}"/> so signing key and issuer track configuration
/// reloads per request scope.
/// </remarks>
internal sealed class FormAccessTokenService : IFormAccessTokenService
{
    /// <summary>HS256 requires at least 256 bits (32 bytes) of key material; we validate UTF-8-encoded length.</summary>
    private const int MinSigningKeyUtf8ByteLength = 32;

    private static readonly TimeSpan _clockSkew = TimeSpan.FromSeconds(15);

    private readonly EndatixJwtOptions _options;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly TokenValidationParameters _validationParameters;
    private readonly SigningCredentials _signingCredentials;

    public FormAccessTokenService(IOptionsSnapshot<EndatixJwtOptions> options, IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.Null(options);
        Guard.Against.Null(dateTimeProvider);

        _options = options.Value;
        _dateTimeProvider = dateTimeProvider;

        Guard.Against.NullOrWhiteSpace(_options.SigningKey);
        Guard.Against.NegativeOrZero(_options.FormAccessTokenExpiryMinutes);

        var signingKeyBytes = Encoding.UTF8.GetBytes(_options.SigningKey);
        if (signingKeyBytes.Length < MinSigningKeyUtf8ByteLength)
        {
            throw new ArgumentException(
                "Form access token signing key must provide at least 256 bits (32 bytes) of key material when UTF-8 encoded (HS256).",
                nameof(options));
        }

        Guard.Against.NullOrEmpty(_options.Audiences);
        Guard.Against.NullOrWhiteSpace(_options.ReBacIssuer);

        var signingKey = new SymmetricSecurityKey(signingKeyBytes);
        _signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        _validationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = signingKey,
            ValidIssuers = [_options.ReBacIssuer],
            ValidAudiences = _options.Audiences,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = _clockSkew
        };
    }

    private static JwtSecurityTokenHandler CreateTokenHandler()
    {
        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };
        return handler;
    }

    /// <inheritdoc />
    public Result<FormAccessTokenDto> CreateToken(long formId, long tenantId)
    {
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NegativeOrZero(tenantId);

        Claim[] claims =
        [
            new Claim(JwtReBacClaims.ResourceType, JwtReBacClaims.ResourceTypeValueForm),
            new Claim(JwtReBacClaims.ResourceId, formId.ToString()),
            new Claim(ClaimNames.TenantId, tenantId.ToString())
        ];

        var expiresUtc = _dateTimeProvider.UtcNow.AddMinutes(_options.FormAccessTokenExpiryMinutes).UtcDateTime;
        JwtSecurityToken token = new(
            issuer: _options.ReBacIssuer,
            audience: _options.Audiences[0],
            claims: claims,
            expires: expiresUtc,
            signingCredentials: _signingCredentials);

        var tokenString = CreateTokenHandler().WriteToken(token);
        return Result.Success(new FormAccessTokenDto(tokenString, expiresUtc));
    }

    /// <inheritdoc />
    public Result<FormAccessTokenClaims> ValidateToken(string token)
    {
        Guard.Against.NullOrWhiteSpace(token);

        try
        {
            var principal = CreateTokenHandler().ValidateToken(token, _validationParameters, out var validatedToken);
            if (validatedToken is not JwtSecurityToken jwtToken)
            {
                return Result.Invalid(FormAccessTokenErrors.ValidationErrors.InvalidToken);
            }

            var resourceType = principal.FindFirst(JwtReBacClaims.ResourceType)?.Value;
            if (resourceType != JwtReBacClaims.ResourceTypeValueForm)
            {
                return Result.Invalid(FormAccessTokenErrors.ValidationErrors.InvalidToken);
            }

            var formIdRaw = principal.FindFirst(JwtReBacClaims.ResourceId)?.Value;
            if (!long.TryParse(formIdRaw, out var formId) || formId <= 0)
            {
                return Result.Invalid(FormAccessTokenErrors.ValidationErrors.InvalidToken);
            }

            var tidRaw = principal.FindFirst(ClaimNames.TenantId)?.Value;
            if (!long.TryParse(tidRaw, out var tenantId) || tenantId <= 0)
            {
                return Result.Invalid(FormAccessTokenErrors.ValidationErrors.InvalidToken);
            }

            var expiresAtUtc = jwtToken.ValidTo.ToUniversalTime();
            return Result.Success(new FormAccessTokenClaims(formId, tenantId, expiresAtUtc));
        }
        catch (SecurityTokenExpiredException)
        {
            return Result.Invalid(FormAccessTokenErrors.ValidationErrors.TokenExpired);
        }
        catch (SecurityTokenException)
        {
            return Result.Invalid(FormAccessTokenErrors.ValidationErrors.InvalidToken);
        }
    }
}
