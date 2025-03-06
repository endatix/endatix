using System;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Ardalis.GuardClauses;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Microsoft.Extensions.Logging;
using Endatix.Framework.Hosting;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring Endatix security.
/// </summary>
public class EndatixSecurityBuilder
{
    private readonly EndatixBuilder _parentBuilder;
    private readonly ILogger? _logger;

    private const int JWT_CLOCK_SKEW_IN_SECONDS = 15;

    /// <summary>
    /// Initializes a new instance of the EndatixSecurityBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent builder.</param>
    internal EndatixSecurityBuilder(EndatixBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
        _logger = parentBuilder.LoggerFactory?.CreateLogger<EndatixSecurityBuilder>();
    }

    /// <summary>
    /// Configures security with default settings.
    /// </summary>
    /// <returns>The security builder for chaining.</returns>
    public EndatixSecurityBuilder UseDefaults()
    {
        // Configure default security settings
        UseJwtAuthentication();
        AddDefaultAuthorization();

        return this;
    }

    /// <summary>
    /// Configures JWT authentication with default settings.
    /// This is the primary method for configuring JWT authentication in the application.
    /// </summary>
    /// <param name="configure">Optional action to configure JWT options.</param>
    /// <returns>The security builder for chaining.</returns>
    public EndatixSecurityBuilder UseJwtAuthentication(Action<JwtBearerOptions>? configure = null)
    {
        var configuration = _parentBuilder.Configuration;
        var appEnvironment = _parentBuilder.AppEnvironment;
        var services = _parentBuilder.Services;

        Guard.Against.Null(configuration, nameof(configuration), "Configuration is required for JWT authentication");

        LogSetupInfo("Configuring JWT authentication");

        // Check if JWT is already configured
        if (services.Any(sd => sd.ServiceType == typeof(JwtBearerOptions)))
        {
            LogSetupInfo("JWT authentication is already configured");
            return this;
        }

        // Register JWT-specific services from Endatix.Infrastructure
        services.AddEndatixJwtServices(configuration);

        var jwtSettings = configuration.GetRequiredSection(JwtOptions.SECTION_NAME).Get<JwtOptions>();
        Guard.Against.Null(jwtSettings, nameof(jwtSettings), "JWT settings are required for authentication");

        var isDevelopment = appEnvironment?.IsDevelopment() ?? false;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Apply default configuration
                options.RequireHttpsMetadata = !isDevelopment;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudiences = jwtSettings.Audiences,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(JWT_CLOCK_SKEW_IN_SECONDS)
                };

                // Apply custom configuration if provided
                configure?.Invoke(options);
            });

        LogSetupInfo("JWT authentication configured successfully");
        return this;
    }

    /// <summary>
    /// Customizes JWT authentication with advanced settings.
    /// </summary>
    /// <param name="configure">The action to configure token validation parameters.</param>
    /// <returns>The security builder for chaining.</returns>
    public EndatixSecurityBuilder WithJwtAuthentication(Action<TokenValidationParameters> configure)
    {
        var configuration = _parentBuilder.Configuration;
        Guard.Against.Null(configuration, nameof(configuration), "Configuration is required for JWT authentication");

        var jwtSettings = configuration.GetRequiredSection(JwtOptions.SECTION_NAME).Get<JwtOptions>();
        Guard.Against.Null(jwtSettings, nameof(jwtSettings), "JWT settings are required for authentication");

        return UseJwtAuthentication(options =>
        {
            // Apply the custom configuration to the token validation parameters
            configure(options.TokenValidationParameters);
        });
    }

    /// <summary>
    /// Adds default authorization policies.
    /// </summary>
    /// <returns>The security builder for chaining.</returns>
    public EndatixSecurityBuilder AddDefaultAuthorization()
    {
        LogSetupInfo("Adding default authorization policies");

        _parentBuilder.Services.AddAuthorization();

        LogSetupInfo("Default authorization policies added");
        return this;
    }

    /// <summary>
    /// Adds custom authorization policies.
    /// </summary>
    /// <param name="configure">The action to configure authorization options.</param>
    /// <returns>The security builder for chaining.</returns>
    public EndatixSecurityBuilder AddAuthorization(Action<Microsoft.AspNetCore.Authorization.AuthorizationOptions> configure)
    {
        LogSetupInfo("Adding custom authorization policies");

        _parentBuilder.Services.AddAuthorization(configure);

        LogSetupInfo("Custom authorization policies added");
        return this;
    }

    /// <summary>
    /// Returns to the parent builder.
    /// </summary>
    /// <returns>The parent builder for chaining.</returns>
    public EndatixBuilder Build() => _parentBuilder;

    private void LogSetupInfo(string message)
    {
        _logger?.LogInformation("{Message}", message);
    }
}