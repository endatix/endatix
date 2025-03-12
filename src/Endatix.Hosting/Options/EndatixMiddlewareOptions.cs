using Endatix.Api.Builders;
using Microsoft.AspNetCore.Builder;

namespace Endatix.Hosting.Options;

/// <summary>
/// Options for configuring Endatix middleware.
/// </summary>
public class EndatixMiddlewareOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to use exception handling middleware.
    /// </summary>
    public bool UseExceptionHandler { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use API middleware.
    /// </summary>
    public bool UseApi { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use security middleware.
    /// </summary>
    public bool UseSecurity { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use multitenancy middleware.
    /// </summary>
    public bool UseMultitenancy { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use HSTS middleware.
    /// </summary>
    public bool UseHsts { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use HTTPS redirection middleware.
    /// </summary>
    public bool UseHttpsRedirection { get; set; } = true;

    /// <summary>
    /// Gets or sets the API options for configuring API middleware.
    /// </summary>
    /// <remarks>
    /// This uses the shared ApiOptions class from the Endatix.Api package
    /// to avoid duplication of options.
    /// </remarks>
    public ApiOptions ApiOptions { get; set; } = new ApiOptions();

    /// <summary>
    /// Gets or sets a delegate to configure additional middleware.
    /// </summary>
    public Action<IApplicationBuilder>? ConfigureAdditionalMiddleware { get; set; }
} 