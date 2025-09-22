using System.IdentityModel.Tokens.Jwt;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Framework.Configuration;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Users;
using Endatix.Infrastructure.Identity.EmailVerification;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Endatix.Infrastructure.Features.Account;
using Endatix.Core.Abstractions.Account;
using Microsoft.AspNetCore.Authentication;
using Endatix.Infrastructure.Identity.Services;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// Extension methods for configuring identity services in the dependency injection container.
/// </summary>
public static class IdentityServiceCollectionExtensions
{
    private const int DEFAULT_PASSWORD_RESET_TOKEN_EXPIRATION_IN_MINUTES = 120;

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
                    // identityOptions.Password.RequireDigit = true;
                    // identityOptions.Password.RequireLowercase = true;
                    // identityOptions.Password.RequireUppercase = true;
                    // identityOptions.Password.RequireNonAlphanumeric = true;
                    // identityOptions.Password.RequiredLength = 8;

                    // identityOptions.SignIn.RequireConfirmedEmail = true;
                    // identityOptions.User.RequireUniqueEmail = true;
                })
                .AddRoles<AppRole>()
                .AddEntityFrameworkStores<AppIdentityDbContext>()
                .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromMinutes(DEFAULT_PASSWORD_RESET_TOKEN_EXPIRATION_IN_MINUTES);
        });

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
    /// Adds all Endatix security infrastructure services (identity, authentication, JWT, authorization).
    /// This replaces AddEndatixIdentityEssentialServices and AddEndatixJwtServices.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEndatixSecurityServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register essential identity services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, AppUserService>();
        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<IPermissionService, PermissionService>();

        // Register claims transformation to enrich JWT with permissions and roles from database
        services.AddTransient<IClaimsTransformation, JwtClaimsTransformer>();

        services.AddHttpContextAccessor();
        services.AddMemoryCache(); // For entity ownership caching
        services.AddScoped<IAuthorizationHandler, PermissionsHandler>();

        // Register security related domain services
        services.AddScoped<IUserRegistrationService, AppUserRegistrationService>();
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();
        services.AddScoped<IUserPasswordManageService, UserPasswordManageService>();
        services.AddScoped<IUserTokenService, JwtTokenService>();
        services.AddScoped<IRepository<EmailVerificationToken>, EmailVerificationTokenRepository>();

        // Register email verification options
        services.AddOptions<EmailVerificationOptions>()
            .BindConfiguration(EndatixOptionsBase.GetSectionName<EmailVerificationOptions>())
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register Auth Options
        services.AddOptions<AuthOptions>()
                .BindConfiguration(AuthOptions.SECTION_NAME)
                .ValidateDataAnnotations()
                .ValidateOnStart();

        return services;
    }
}