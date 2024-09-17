using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Auth;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        endatixApp.Services.AddAuthorization();

        endatixApp.Services
                .AddAuthentication(options => options.DefaultScheme = "Cookies")
                .AddCookie(IdentityConstants.ApplicationScheme);

        endatixApp.Services
                .AddIdentityCore<AppUser>()
                .AddEntityFrameworkStores<AppIdentityDbContext>()
                .AddDefaultTokenProviders();

        // TODO: Move this to Fast Endpoints
        endatixApp.Services.AddAuthenticationJwtBearer(s => s.SigningKey = signingKey);

        endatixApp.Services.AddScoped<ITokenService, JwtTokenService>();
        endatixApp.LogSetupInformation("     >> Registering core authentication services");
        endatixApp.Services.AddScoped<IAuthService, ConfigBasedAuthService>();
        endatixApp.LogSetupInformation("     >> Registering {Interface} using the {ClassName} class", typeof(IAuthService).Name, typeof(ConfigBasedAuthService).Name);

        endatixApp.LogSetupInformation("{Component} infrastructure configuration | {Status}", "Security Config", "Finished");

        return endatixApp;
    }
}
