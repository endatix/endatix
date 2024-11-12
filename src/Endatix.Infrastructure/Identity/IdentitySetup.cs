using System.IdentityModel.Tokens.Jwt;
using Endatix.Core.Abstractions;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

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
                .AddIdentityCore<AppUser>(options =>
                {
                    options.ClaimsIdentity.UserIdClaimType = JwtRegisteredClaimNames.Sub;
                })
                .AddRoles<AppRole>()
                .AddEntityFrameworkStores<AppIdentityDbContext>()
                .AddDefaultTokenProviders();

        endatixApp.Services.AddScoped<ITokenService, JwtTokenService>();
        endatixApp.Services.AddScoped<IAuthService, AuthService>();
        endatixApp.Services.AddScoped<IUserService, AppUserService>();
        endatixApp.Services.AddScoped<IUserRegistrationService, AppUserRegistrationService>();

        endatixApp.LogSetupInformation("{Component} infrastructure configuration | {Status}", "Security Config", "Finished");

        return endatixApp;
    }
}
