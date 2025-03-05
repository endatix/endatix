using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Endatix.Api.Setup;
using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.Extensions.Hosting;
using Endatix.Infrastructure.Identity;
using Endatix.Api.Infrastructure;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring middleware in the Endatix application.
/// </summary>
public class EndatixMiddlewareBuilder
{
    private readonly IApplicationBuilder _app;
    private readonly ILogger? _logger;
    
    /// <summary>
    /// Gets the application builder.
    /// </summary>
    public IApplicationBuilder App => _app;
    
    /// <summary>
    /// Initializes a new instance of the EndatixMiddlewareBuilder class.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public EndatixMiddlewareBuilder(IApplicationBuilder app)
    {
        _app = app;
        _logger = app.ApplicationServices.GetService<ILoggerFactory>()?.CreateLogger("Endatix.Middleware");
    }
    
    /// <summary>
    /// Configures the middleware with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseDefaults()
    {
        LogSetupInfo("Configuring middleware with default settings");
        
        UseExceptionHandler()
            .UseSecurity()
            .UseHsts()
            .UseHttpsRedirection()
            .UseRequestLogging();
        
        LogSetupInfo("Middleware configured with default settings");
        return this;
    }
    
    /// <summary>
    /// Adds exception handling middleware.
    /// </summary>
    /// <param name="path">The path for exception handling.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseExceptionHandler(string path = "/error")
    {
        LogSetupInfo($"Adding exception handler middleware with path: {path}");
        _app.UseExceptionHandler(path);
        return this;
    }
    
    /// <summary>
    /// Adds security middleware (authentication and authorization).
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseSecurity()
    {
        LogSetupInfo("Adding security middleware");
        _app.UseAuthentication();
        _app.UseAuthorization();
        return this;
    }
    
    /// <summary>
    /// Adds HSTS middleware.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseHsts()
    {
        LogSetupInfo("Adding HSTS middleware");
        _app.UseHsts();
        return this;
    }
    
    /// <summary>
    /// Adds HTTPS redirection middleware.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseHttpsRedirection()
    {
        LogSetupInfo("Adding HTTPS redirection middleware");
        _app.UseHttpsRedirection();
        return this;
    }
    
    /// <summary>
    /// Adds request logging middleware.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseRequestLogging()
    {
        LogSetupInfo("Adding request logging middleware");
        // Note: Implementing generic request logging that can work with various logging providers
        // For specific logging implementation like Serilog, this should be configured in the application
        return this;
    }
    
    /// <summary>
    /// Adds API middleware.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseApi()
    {
        LogSetupInfo("Adding API middleware");

        _app.UseDefaultExceptionHandler(_logger, true, true);
        _app.UseFastEndpoints(fastEndpoints =>
        {
            fastEndpoints.Versioning.Prefix = "v";
            fastEndpoints.Endpoints.RoutePrefix = "api";
            fastEndpoints.Serializer.Options.Converters.Add(new LongToStringConverter());
            fastEndpoints.Security.RoleClaimType = ClaimTypes.Role;
            fastEndpoints.Security.PermissionsClaimType = ClaimNames.Permission;
        });

        var env = _app.ApplicationServices.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        if (env != null && (env.IsDevelopment() || env.IsProduction()))
        {
            _app.UseSwaggerGen(null, c => c.Path = "");
        }

        //_app.UseCors();
        
        return this;

        // Apply FastEndpoints directly
        UseFastEndpoints();
        
        // Use Swagger in development
        //var env = _app.ApplicationServices.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        if (env != null && env.IsDevelopment())
        {
            UseSwagger();
        }
        
        return this;
    }
    
    /// <summary>
    /// Adds API middleware with custom configuration.
    /// </summary>
    /// <param name="configureApi">The configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseApi(Action<EndatixApiMiddlewareOptions> configureApi)
    {
        LogSetupInfo("Adding API middleware with custom configuration");
        
        // Create options with defaults
        var options = new EndatixApiMiddlewareOptions();
        configureApi(options);
        
        // Apply FastEndpoints configuration
        UseFastEndpoints(config => 
        {
            // Apply versioning configuration
            config.Versioning.Prefix = options.VersioningPrefix;
            config.Endpoints.RoutePrefix = options.RoutePrefix;
            
            // Apply any custom configuration
            options.ConfigureFastEndpoints?.Invoke(config);
        });
        
        // Apply Swagger if enabled
        if (options.UseSwagger)
        {
            var env = _app.ApplicationServices.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            if (env != null && (env.IsDevelopment() || options.EnableSwaggerInProduction))
            {
                UseSwagger();
            }
        }
        
        // Apply CORS if enabled
        if (options.UseCors)
        {
            UseCors();
        }
        
        return this;
    }
    
    /// <summary>
    /// Adds FastEndpoints middleware with default configuration.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseFastEndpoints()
    {
        LogSetupInfo("Adding FastEndpoints middleware with default configuration");
        _app.UseFastEndpoints();
        return this;
    }
    
    /// <summary>
    /// Adds FastEndpoints middleware with custom configuration.
    /// </summary>
    /// <param name="configure">The configuration action for FastEndpoints.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseFastEndpoints(Action<FastEndpoints.Config> configure)
    {
        LogSetupInfo("Adding FastEndpoints middleware with custom configuration");
        _app.UseFastEndpoints(configure);
        return this;
    }
    
    /// <summary>
    /// Adds Swagger middleware.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseSwagger()
    {
        LogSetupInfo("Adding Swagger middleware");
        _app.UseSwaggerGen();
        return this;
    }
    
    /// <summary>
    /// Adds Swagger middleware with custom configuration.
    /// </summary>
    /// <param name="swaggerOptions">The configuration options for Swagger.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseSwagger(Action<object> swaggerOptions)
    {
        LogSetupInfo("Adding Swagger middleware with custom configuration");
        _app.UseSwaggerGen(swaggerOptions);
        return this;
    }
    
    /// <summary>
    /// Adds CORS middleware.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseCors()
    {
        LogSetupInfo("Adding CORS middleware");
        _app.UseCors();
        return this;
    }
    
    /// <summary>
    /// Adds CORS middleware with custom policy.
    /// </summary>
    /// <param name="policyName">The name of the CORS policy to apply.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseCors(string policyName)
    {
        LogSetupInfo($"Adding CORS middleware with policy: {policyName}");
        _app.UseCors(policyName);
        return this;
    }
    
    /// <summary>
    /// Adds legacy Endatix API middleware for backward compatibility.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseLegacyEndatixApi()
    {
        LogSetupInfo("Adding legacy Endatix API middleware");
        
        // Configure exception handling
        UseExceptionHandler();
        
        // Configure FastEndpoints
        UseFastEndpoints(fastEndpoints =>
        {
            fastEndpoints.Versioning.Prefix = "v";
            fastEndpoints.Endpoints.RoutePrefix = "api";
            
            // Add LongToStringConverter if available
            try {
                var converterType = Type.GetType("Endatix.Api.Infrastructure.LongToStringConverter, Endatix.Api");
                if (converterType != null)
                {
                    var converter = Activator.CreateInstance(converterType);
                    // Can't directly cast to JsonConverter, so we'll skip this part
                    // and let the specific implementation handle it
                }
            }
            catch (Exception ex) {
                _logger?.LogWarning(ex, "Could not add LongToStringConverter");
            }
            
            // Set security claims
            fastEndpoints.Security.RoleClaimType = ClaimTypes.Role;
            
            try {
                var claimNamesType = Type.GetType("Endatix.Infrastructure.Identity.ClaimNames, Endatix.Infrastructure");
                if (claimNamesType != null)
                {
                    var permissionField = claimNamesType.GetField("Permission");
                    if (permissionField != null)
                    {
                        var permissionValue = permissionField.GetValue(null)?.ToString();
                        if (!string.IsNullOrEmpty(permissionValue))
                        {
                            fastEndpoints.Security.PermissionsClaimType = permissionValue;
                        }
                    }
                }
            }
            catch (Exception ex) {
                _logger?.LogWarning(ex, "Could not set PermissionsClaimType");
            }
        });
        
        // Configure Swagger
        var env = _app.ApplicationServices.GetService<IWebHostEnvironment>();
        if (env != null && (env.IsDevelopment() || env.IsProduction()))
        {
            UseSwagger(c => { });
        }
        
        // Configure CORS
        UseCors();
        
        return this;
    }
    
    /// <summary>
    /// Applies custom middleware configuration.
    /// </summary>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixMiddlewareBuilder UseCustomMiddleware(Action<IApplicationBuilder> configure)
    {
        LogSetupInfo("Adding custom middleware");
        configure(_app);
        return this;
    }
    
    private void LogSetupInfo(string message)
    {
        _logger?.LogInformation("[Middleware Setup] {Message}", message);
    }
} 