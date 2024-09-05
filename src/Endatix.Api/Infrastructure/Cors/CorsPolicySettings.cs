namespace Endatix.Api.Infrastructure.Cors;

/// <summary>
/// Class used to define CORS policies via JSON in appSettings config files.
/// </summary>
public class CorsPolicySettings
{
    /// <summary>
    /// The CORS policy name. Add unique and intuitive name
    /// </summary>
    public string PolicyName { get; set; } = "DefaultPolicy";

    /// <summary>
    /// Gets or sets the list of origins that should be allowed to make cross-origin calls
    /// <example>["https://foo.com", "foo.io"]</example>
    /// Use "*" to allow all origins.
    /// Use "-" to disallow all origins
    /// </summary>
    public IList<string> AllowedOrigins { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of methods that should be allowed for making cross-origin calls
    /// <example>["GET", "POST"]</example>
    /// Use "*" to allow all methods.
    /// Use "-" to disallow all methods
    /// </summary>
    public IList<string> AllowedMethods { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of headers that should be allowed for making cross-origin calls
    /// <example>["Accept", "Accept-Encoding"]</example>
    /// Use "*" to allow all headers.
    /// Use "-" to disallow all headers
    /// </summary>
    public IList<string> AllowedHeaders { get; set; } = [];

    /// <summary>
    /// By default, the browser doesn't expose all of the response headers to the app. The response headers that are available by default are:
    /// Cache-Control
    /// Content-Language
    /// Content-Type
    /// Expires
    /// Last-Modified
    /// Pragma
    /// These are called "simple response headers." <see cref="https://www.w3.org/TR/cors/#simple-response-header"/>
    /// Populate this to add additional headers to the app
    /// <example>["X-Forwarded-For", "X-Forwarded-Host", "x-custom-header"]</example>
    /// </summary>
    public IList<string> ExposedHeaders { get; set; } = [];

    /// <summary>
    ///Add seconds to form the Access-Control-Max-Age header, which specifies how long the response to the preflight request can be cached. Add positive number in seconds
    /// </summary>
    public uint PreflightMaxAgeInSeconds { get; set; }

    /// <summary>
    /// Set this to true to allow cross-origin credentials. False by default as it adds attack surface especially when users with AllowAnyOrigin
    /// Note: Specifying AllowAnyOrigin and AllowCredentials is an insecure configuration and can result in cross-site request forgery. The CORS service returns an invalid CORS response when an app is configured with both methods.
    /// </summary>
    public bool AllowCredentials { get; set; }
}
