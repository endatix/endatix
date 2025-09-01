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
    public void Configure(AuthenticationBuilder builder, IConfigurationSection providerConfig, bool isDevelopment = false)
    {
        var googleOptions = providerConfig.Get<GoogleOptions>();
        Guard.Against.Null(googleOptions);

        if (!googleOptions.Enabled)
        {
            return;
        }

        var googleIssuer = googleOptions.Issuer;
        Guard.Against.NullOrEmpty(googleIssuer, nameof(GoogleOptions.Issuer));

        _cachedIssuer = googleIssuer;

        builder.AddJwtBearer(SchemeName, options =>
                   {
                       options.Authority = googleIssuer;
                       options.RequireHttpsMetadata = true;
                       options.TokenValidationParameters = new TokenValidationParameters
                       {
                           ValidateIssuer = true,
                           ValidIssuers = new[] { googleOptions.Issuer },
                           ValidateAudience = true,
                           ValidAudience = googleOptions.Audience,
                           ValidateLifetime = true,
                           ClockSkew = TimeSpan.FromMinutes(2),
                           NameClaimType = "email",
                       };
                   });

    }
}