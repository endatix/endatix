using System;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring Endatix messaging.
/// </summary>
public class EndatixMessagingBuilder
{
    private readonly EndatixBuilder _parentBuilder;
    
    /// <summary>
    /// Initializes a new instance of the EndatixMessagingBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent builder.</param>
    internal EndatixMessagingBuilder(EndatixBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
    }
    
    /// <summary>
    /// Configures messaging with default settings.
    /// </summary>
    /// <returns>The messaging builder for chaining.</returns>
    public EndatixMessagingBuilder UseDefaults()
    {
        // Configure default messaging settings
        UsePipelineLogging();
        
        return this;
    }
    
    /// <summary>
    /// Enables logging of message pipelines.
    /// </summary>
    /// <returns>The messaging builder for chaining.</returns>
    public EndatixMessagingBuilder UsePipelineLogging()
    {
        // Configure pipeline logging
        
        return this;
    }
    
    /// <summary>
    /// Configures MediatR.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for handlers.</param>
    /// <returns>The messaging builder for chaining.</returns>
    public EndatixMessagingBuilder AddMediatR(params System.Reflection.Assembly[] assemblies)
    {
        // Register MediatR services
        
        return this;
    }
    
    /// <summary>
    /// Gets the parent builder.
    /// </summary>
    /// <returns>The parent builder.</returns>
    public EndatixBuilder Parent() => _parentBuilder;
} 