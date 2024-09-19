using System.Text;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Framework.Hosting;
using Endatix.Identity.Authentication;
using Endatix.Infrastructure.Auth;
using Endatix.Infrastructure.Identity.Registration;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Endatix.Infrastructure.Identity;

public static class IdentitySetup
{
    public static IEndatixApp SetupIdentity(this IEndatixApp endatixApp, ConfigurationOptions configurationOptions)
    {
        endatixApp.LogSetupInformation("{Component} infrastructure configuration | {Status}", "Security Config", "Started");

        var securityConfig = configurationOptions.Security.SecurityConfiguration;
        endatixApp.Services.Configure<SecuritySettings>(securityConfig);

        var signingKey = securityConfig.GetRequiredSection(nameof(SecuritySettings.JwtSigningKey)).Value;
        Guard.Against.NullOrEmpty(signingKey, "signingKey", $"Cannot initialize application without a signingKey. Please check configuration for {nameof(SecuritySettings.JwtSigningKey)}");


        endatixApp.Services
                .AddIdentityCore<AppUser>()
                .AddEntityFrameworkStores<AppIdentityDbContext>()
                .AddDefaultTokenProviders();


        // TODO: Move this to Fast Endpoints
        endatixApp.Services.AddAuthenticationJwtBearer(
            signingOptions => signingOptions.SigningKey = signingKey,
            bearerOptions =>
            {
                bearerOptions.RequireHttpsMetadata = false;
                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidIssuer = "configuration['JwtSettings:Issuer']",
                    ValidAudience = "configuration['JwtSettings:Audience']",
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(15)
                };
            }
        );
        endatixApp.Services.AddAuthentication(options => options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme);
        endatixApp.Services.AddAuthorization();
        endatixApp.Services.AddScoped<ITokenService, JwtTokenService>();
        endatixApp.Services.AddScoped<IAuthService, AuthService>();
        endatixApp.Services.AddScoped<IUserRegistrationService, UserRegistrationService>();

        endatixApp.LogSetupInformation("{Component} infrastructure configuration | {Status}", "Security Config", "Finished");

        return endatixApp;
    }
}
