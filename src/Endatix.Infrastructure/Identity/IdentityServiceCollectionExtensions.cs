using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// Extension methods for configuring identity services in the application.
/// </summary>
public static class IdentityServiceCollectionExtensions
{
    /// <summary>
    /// Adds default identity configuration to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIdentityConfiguration(this IServiceCollection services)
    {
        return services.AddIdentityConfiguration(new ConfigurationOptions());
    }

    /// <summary>
    /// Adds identity configuration with custom options to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The custom configuration options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIdentityConfiguration(this IServiceCollection services, ConfigurationOptions options)
    {
        services.AddIdentityCore<AppUser>(identityOptions =>
                {
                    identityOptions.ClaimsIdentity.UserIdClaimType = JwtRegisteredClaimNames.Sub;
                    identityOptions.Password.RequireDigit = true;
                    identityOptions.Password.RequireLowercase = true;
                    identityOptions.Password.RequireUppercase = true;
                    identityOptions.Password.RequireNonAlphanumeric = true;
                    identityOptions.Password.RequiredLength = 8;

                    identityOptions.SignIn.RequireConfirmedEmail = true;
                    identityOptions.User.RequireUniqueEmail = true;
                })
                .AddRoles<AppRole>()
                .AddEntityFrameworkStores<AppIdentityDbContext>()
                .AddDefaultTokenProviders();

        // Register essential identity services
        services.AddEndatixIdentityEssentialServices(options.UseJwtAuthentication);

        if (options.InitialUserSettings != null)
        {
            services.Configure<InitialUserOptions>(config =>
            {
                config.Email = options.InitialUserSettings.Email;
                config.Password = options.InitialUserSettings.Password;
            });
        }

        return services;
    }

    /// <summary>
    /// Adds essential Endatix identity services that are required regardless of how the application is configured.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="useJwtAuthentication">Whether to use JWT authentication. Defaults to true for backward compatibility.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEndatixIdentityEssentialServices(this IServiceCollection services, bool useJwtAuthentication = true)
    {
        // Register essential identity services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, AppUserService>();
        services.AddScoped<IUserRegistrationService, AppUserRegistrationService>();
        
        if (useJwtAuthentication)
        {
            // Register JWT options
            services.AddOptions<JwtOptions>()
                    .BindConfiguration(JwtOptions.SECTION_NAME)
                    .ValidateDataAnnotations()
                    .ValidateOnStart();
            services.AddScoped<IUserTokenService, JwtTokenService>();
        }
        
        return services;
    }
}