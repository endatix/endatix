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
/// Builder for configuring Endatix security features including authentication and authorization.
/// </summary>
/// <remarks>
/// The EndatixSecurityBuilder provides a fluent API for configuring security-related aspects of your application:
/// <list type="bullet">
/// <item><description>JWT authentication</description></item>
/// <item><description>Custom token validation</description></item>
/// <item><description>Role-based and policy-based authorization</description></item>
/// </list>
/// 
/// You typically obtain an instance of this builder through the <see cref="EndatixBuilder.Security"/> property.
/// </remarks>
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
    /// <remarks>
    /// This method applies sensible defaults for security:
    /// <list type="bullet">
    /// <item><description>JWT authentication with settings from configuration</description></item>
    /// <item><description>Standard authorization policies</description></item>
    /// </list>
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Configure security with defaults
    /// builder.Host.UseEndatix(endatix => endatix
    ///     .WithSecurity(security => security
    ///         .UseDefaults()));
    /// </code>
    /// </example>
    /// </remarks>
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
    /// <remarks>
    /// This method configures JWT authentication with sensible defaults:
    /// <list type="bullet">
    /// <item><description>Token validation parameters configured from appsettings.json</description></item>
    /// <item><description>Secure defaults for token lifetime and validation</description></item>
    /// <item><description>Claims-based identity</description></item>
    /// </list>
    /// 
    /// By default, it looks for JWT settings in the "Endatix:Security:Jwt" section of your configuration:
    /// 
    /// <code>
    /// {
    ///   "Endatix": {
    ///     "Security": {
    ///       "Jwt": {
    ///         "SigningKey": "your-secret-key-at-least-32-characters",
    ///         "Issuer": "endatix",
    ///         "Audience": "endatix-clients",
    ///         "ExpirationMinutes": 60
    ///       }
    ///     }
    ///   }
    /// }
    /// </code>
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Configure JWT authentication with defaults
    /// builder.Host.UseEndatix(endatix => endatix
    ///     .WithSecurity(security => security
    ///         .UseJwtAuthentication()));
    ///     
    /// // Or with custom options
    /// builder.Host.UseEndatix(endatix => endatix
    ///     .WithSecurity(security => security
    ///         .UseJwtAuthentication(options => 
    ///         {
    ///             options.TokenValidationParameters.ValidateIssuer = false;
    ///             options.TokenValidationParameters.ValidateAudience = false;
    ///         })));
    /// </code>
    /// </example>
    /// </remarks>
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

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
                options.MapInboundClaims = false;

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
    /// <remarks>
    /// This method configures the default ASP.NET Core authorization system without any custom policies.
    /// It's suitable for simple applications where you only need to check if a user is authenticated.
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Add Endatix with default authorization
    /// builder.Services.AddEndatix(builder.Configuration)
    ///     .Security
    ///     .UseJwtAuthentication()
    ///     .AddDefaultAuthorization();
    ///     
    /// var app = builder.Build();
    /// 
    /// // Use authentication and authorization middleware
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// </code>
    /// </example>
    /// </remarks>
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
    /// <remarks>
    /// This method allows you to define custom authorization policies for your application.
    /// Policies can be based on claims, roles, or custom requirements.
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Add Endatix with custom authorization policies
    /// builder.Services.AddEndatix(builder.Configuration)
    ///     .Security
    ///     .UseJwtAuthentication()
    ///     .AddAuthorization(options => 
    ///     {
    ///         // Add a policy requiring the 'admin' role
    ///         options.AddPolicy("RequireAdminRole", policy => 
    ///             policy.RequireRole("admin"));
    ///             
    ///         // Add a policy requiring a specific claim
    ///         options.AddPolicy("PremiumUsers", policy => 
    ///             policy.RequireClaim("subscription", "premium"));
    ///     });
    ///     
    /// var app = builder.Build();
    /// 
    /// // Use authentication and authorization middleware
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// 
    /// // Use the policy in an endpoint
    /// app.MapGet("/admin", () => "Admin area")
    ///     .RequireAuthorization("RequireAdminRole");
    /// </code>
    /// </example>
    /// </remarks>
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
    /// Builds and returns the parent builder.
    /// </summary>
    /// <returns>The parent builder.</returns>
    public EndatixBuilder Build() => _parentBuilder;

    private void LogSetupInfo(string message)
    {
        _logger?.LogDebug("[Security Setup] {Message}", message);
    }
}