using Microsoft.AspNetCore.Builder;

namespace Endatix.Hosting.Core;

/// <summary>
/// Defines the contract for configuring Endatix middleware.
/// </summary>
public interface IEndatixMiddleware
{
    /// <summary>
    /// Gets the WebApplication instance on which middleware is configured.
    /// </summary>
    WebApplication App { get; }
} 