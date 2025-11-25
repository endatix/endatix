using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Endatix.Infrastructure.Identity.Authentication.Providers;

public class GoogleAuthProvider : IAuthProvider
{
    private string? _cachedIssuer;
    public string SchemeName => "Google";

    /// <inheritdoc />
    public bool CanHandle(string issuer, string rawToken)
    {
        return issuer == _cachedIssuer;

    }

    /// <inheritdoc />
    public bool Configure(AuthenticationBuilder builder, IConfigurationSection providerConfig, bool isDevelopment = false)
    {
        Guard.Against.Null(builder);
        Guard.Against.Null(providerConfig);

        var googleOptions = providerConfig.Get<GoogleOptions>();
        Guard.Against.Null(googleOptions);

        if (!googleOptions.Enabled)
        {
            return false;
        }

        var googleIssuer = googleOptions.Issuer;
        Guard.Against.NullOrWhiteSpace(googleIssuer, nameof(GoogleOptions.Issuer));

        _cachedIssuer = googleIssuer;

        builder.AddJwtBearer(SchemeName, options =>
            {
                options.Authority = googleIssuer;
                options.RequireHttpsMetadata = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuers = new[] { googleIssuer },
                    ValidateAudience = true,
                    ValidAudience = googleOptions.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    NameClaimType = "email",
                };
            });

        return true;
    }
}