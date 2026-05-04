using System.Text;
using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Endatix.Infrastructure.Identity.Authentication.Providers;

/// <summary>
/// JWT bearer provider for Endatix hub/user access tokens.
/// </summary>
public sealed class EndatixUserJwtAuthProvider : IAuthProvider
{
    private string? _issuer;

    public string SchemeName => AuthSchemes.EndatixJwt;

    public bool CanHandle(string issuer, string rawToken)
    {
        return _issuer is not null && string.Equals(issuer, _issuer, StringComparison.Ordinal);
    }

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
        Guard.Against.NullOrWhiteSpace(options.SigningKey);

        _issuer = options.Issuer;

        builder.AddJwtBearer(AuthSchemes.EndatixJwt, jwtOptions =>
        {
            jwtOptions.RequireHttpsMetadata = !isDevelopment;
            jwtOptions.MapInboundClaims = options.MapInboundClaims;
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                ValidIssuers = [options.Issuer],
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
