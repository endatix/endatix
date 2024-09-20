using System.Security.Claims;
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

        endatixApp.Services.AddOptions<JwtOptions>()
                .BindConfiguration(JwtOptions.SECTION_NAME)
                .ValidateDataAnnotations()
                .ValidateOnStart();

        endatixApp.Services
                .AddIdentityCore<AppUser>()
                .AddEntityFrameworkStores<AppIdentityDbContext>()
                .AddDefaultTokenProviders();

        endatixApp.Services.AddScoped<ITokenService, JwtTokenService>();
        endatixApp.Services.AddScoped<IAuthService, AuthService>();
        endatixApp.Services.AddScoped<IUserRegistrationService, UserRegistrationService>();

        endatixApp.LogSetupInformation("{Component} infrastructure configuration | {Status}", "Security Config", "Finished");

        return endatixApp;
    }
}
