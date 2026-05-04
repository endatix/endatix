using System.Text;
using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Endatix.Infrastructure.Identity.Authentication.Providers;

/// <summary>
/// JWT bearer provider for public resource based access control(ReBAC). Examples include forms, submissions, data lists, etc. 
/// </summary>
public sealed class EndatixResourceJwtAuthProvider : IAuthProvider
{
    private string? _rebacIssuer;

    /// <inheritdoc />
    public string SchemeName => AuthSchemes.EndatixReBac;

    /// <inheritdoc />
    public string ConfigurationSectionPath => $"Endatix:Auth:Providers:{AuthSchemes.EndatixJwt}";

    /// <inheritdoc />
    public bool CanHandle(string issuer, string rawToken)
    {
        return _rebacIssuer is not null && string.Equals(issuer, _rebacIssuer, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public bool Configure(AuthenticationBuilder builder, IConfigurationSection providerConfig, bool isDevelopment = false)
    {
        Guard.Against.Null(builder);
        Guard.Against.Null(providerConfig);

        var options = providerConfig.Get<EndatixJwtOptions>() ?? throw new InvalidOperationException("EndatixJwt options are required.");
        if (!options.Enabled)
        {
            return false;
        }

        Guard.Against.NullOrWhiteSpace(options.Issuer);
        Guard.Against.NullOrWhiteSpace(options.ReBacIssuer);
        Guard.Against.NullOrWhiteSpace(options.SigningKey);
        if (string.Equals(options.Issuer, options.ReBacIssuer, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "EndatixJwt Issuer and ReBacIssuer must be different to avoid ambiguous scheme selection.");
        }

        _rebacIssuer = options.ReBacIssuer;

        builder.AddJwtBearer(AuthSchemes.EndatixReBac, jwtOptions =>
        {
            jwtOptions.RequireHttpsMetadata = !isDevelopment;
            jwtOptions.MapInboundClaims = options.MapInboundClaims;
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                ValidIssuers = [options.ReBacIssuer],
                ValidAudiences = options.Audiences,
                ValidateIssuer = options.ValidateIssuer,
                ValidateAudience = options.ValidateAudience,
                ValidateLifetime = options.ValidateLifetime,
                ValidateIssuerSigningKey = options.ValidateIssuerSigningKey,
                ClockSkew = TimeSpan.FromSeconds(options.ClockSkewSeconds)
            };
        });

        return true;
    }
}
