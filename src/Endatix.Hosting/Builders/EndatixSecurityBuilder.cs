using System;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring Endatix security.
/// </summary>
public class EndatixSecurityBuilder
{
    private readonly EndatixBuilder _parentBuilder;
    
    /// <summary>
    /// Initializes a new instance of the EndatixSecurityBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent builder.</param>
    internal EndatixSecurityBuilder(EndatixBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
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
    /// Configures JWT authentication.
    /// </summary>
    /// <param name="configure">Optional action to configure JWT options.</param>
    /// <returns>The security builder for chaining.</returns>
    public EndatixSecurityBuilder UseJwtAuthentication(Action<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>? configure = null)
    {
        _parentBuilder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Apply default configuration
                
                // Apply custom configuration if provided
                configure?.Invoke(options);
            });
        
        return this;
    }
    
    /// <summary>
    /// Adds default authorization.
    /// </summary>
    /// <returns>The security builder for chaining.</returns>
    public EndatixSecurityBuilder AddDefaultAuthorization()
    {
        _parentBuilder.Services.AddAuthorization();
        
        return this;
    }
    
    /// <summary>
    /// Adds custom authorization.
    /// </summary>
    /// <param name="configure">Action to configure authorization options.</param>
    /// <returns>The security builder for chaining.</returns>
    public EndatixSecurityBuilder AddAuthorization(Action<Microsoft.AspNetCore.Authorization.AuthorizationOptions> configure)
    {
        _parentBuilder.Services.AddAuthorization(configure);
        
        return this;
    }
    
    /// <summary>
    /// Gets the parent builder.
    /// </summary>
    /// <returns>The parent builder.</returns>
    public EndatixBuilder Parent() => _parentBuilder;
} 