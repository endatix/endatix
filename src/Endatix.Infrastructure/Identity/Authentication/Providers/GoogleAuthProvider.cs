using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Endatix.Infrastructure.Identity.Authentication.Providers;

public class GoogleAuthProvider : IAuthProvider
{
    public const string GOOGLE_ID = "Google";
    public string SchemeName => GOOGLE_ID;

    public void Configure(AuthenticationBuilder builder, IConfigurationSection providerConfig, bool isDevelopment = false)
    {
        var googleOptions = providerConfig.Get<GoogleOptions>();
        Guard.Against.Null(googleOptions);

        if (!googleOptions.Enabled)
        {
            return;
        }

        builder.AddJwtBearer("Google", options =>
                   {
                       options.Authority = googleOptions.RealmUrl;
                       options.RequireHttpsMetadata = true;
                       options.TokenValidationParameters = new TokenValidationParameters
                       {
                           ValidateIssuer = true,
                           ValidIssuers = new[] { googleOptions.RealmUrl },
                           ValidateAudience = true,
                           ValidAudience = googleOptions.Audience,
                           ValidateLifetime = true,
                           ClockSkew = TimeSpan.FromMinutes(2),
                           NameClaimType = "email",
                       };
                   });
    }
}