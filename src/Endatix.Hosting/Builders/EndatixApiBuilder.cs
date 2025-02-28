using System;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring Endatix API settings.
/// </summary>
public class EndatixApiBuilder
{
    private readonly EndatixBuilder _parentBuilder;
    
    /// <summary>
    /// Initializes a new instance of the EndatixApiBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent builder.</param>
    internal EndatixApiBuilder(EndatixBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
    }
    
    /// <summary>
    /// Configures API with default settings.
    /// </summary>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder UseDefaults()
    {
        // Configure default API settings
        AddSwagger();
        AddVersioning();
        
        return this;
    }
    
    /// <summary>
    /// Adds Swagger documentation to the API.
    /// </summary>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder AddSwagger()
    {
        _parentBuilder.Services.AddEndpointsApiExplorer();
    
        return this;
    }
    
    /// <summary>
    /// Adds API versioning support.
    /// </summary>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder AddVersioning()
    {
        // Implement API versioning
        
        return this;
    }
    
    /// <summary>
    /// Enables CORS with the specified policy.
    /// </summary>
    /// <param name="policyName">The CORS policy name.</param>
    /// <param name="configurePolicy">Action to configure the CORS policy.</param>
    /// <returns>The API builder for chaining.</returns>
    public EndatixApiBuilder EnableCors(string policyName, Action<Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder> configurePolicy)
    {
        _parentBuilder.Services.AddCors(options =>
        {
            options.AddPolicy(policyName, configurePolicy);
        });
        
        return this;
    }
    
    /// <summary>
    /// Gets the parent builder.
    /// </summary>
    /// <returns>The parent builder.</returns>
    public EndatixBuilder Parent() => _parentBuilder;
} 