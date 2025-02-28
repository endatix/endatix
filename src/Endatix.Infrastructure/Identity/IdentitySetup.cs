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
    public static IEndatixApp SetupIdentity<TAppIdentityDbContext>(this IEndatixApp endatixApp, ConfigurationOptions configurationOptions)
        where TAppIdentityDbContext : AppIdentityDbContext
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
                .AddEntityFrameworkStores<TAppIdentityDbContext>()
                .AddDefaultTokenProviders();
                
        endatixApp.Services.AddScoped<IUserTokenService, JwtTokenService>();
        endatixApp.Services.AddScoped<IAuthService, AuthService>();
        endatixApp.Services.AddScoped<IUserService, AppUserService>();
        endatixApp.Services.AddScoped<IUserRegistrationService, AppUserRegistrationService>();

        endatixApp.LogSetupInformation("{Component} infrastructure configuration | {Status}", "Security Config", "Finished");

        return endatixApp;
    }

    // // Keep the non-generic version for backward compatibility
    // public static IEndatixApp SetupIdentity(this IEndatixApp endatixApp, ConfigurationOptions configurationOptions)
    //     => SetupIdentity<AppIdentityDbContext>(endatixApp, configurationOptions);
}
