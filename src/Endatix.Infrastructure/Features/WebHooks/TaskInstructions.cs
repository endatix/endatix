namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Represents a set of task instructions for specific webhook operation.
/// </summary>
public class TaskInstructions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaskInstructions"/> class with the specified URI.
    /// </summary>
    /// <param name="uri">The URI associated with the task instruction set.</param>
    public TaskInstructions(string uri)
    {
        Uri = uri;
        Authentication = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskInstructions"/> class with the specified URI and authentication.
    /// </summary>
    /// <param name="uri">The URI associated with the task instruction set.</param>
    /// <param name="authentication">The authentication configuration for the webhook request.</param>
    public TaskInstructions(string uri, AuthenticationConfig? authentication)
    {
        Uri = uri;
        Authentication = authentication;
    }

    /// <summary>
    /// Creates TaskInstructions from a WebHookEndpoint.
    /// </summary>
    /// <param name="endpoint">The webhook endpoint containing URL and authentication configuration.</param>
    /// <returns>A new TaskInstructions instance.</returns>
    public static TaskInstructions FromEndpoint(WebHookEndpoint endpoint)
    {
        return new TaskInstructions(endpoint.Url, endpoint.Authentication);
    }

    /// <summary>
    /// Gets the URI associated with the task instruction set.
    /// </summary>
    public string Uri { get; init; }

    /// <summary>
    /// Gets the authentication configuration for the webhook request.
    /// </summary>
    public AuthenticationConfig? Authentication { get; init; }
}
