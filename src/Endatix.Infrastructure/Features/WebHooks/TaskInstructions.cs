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
    /// Creates TaskInstructions from a WebHookEndpointConfig (database-backed configuration).
    /// </summary>
    /// <param name="endpointConfig">The webhook endpoint configuration from the database.</param>
    /// <returns>A new TaskInstructions instance.</returns>
    public static TaskInstructions FromWebHookEndpointConfig(Endatix.Core.Entities.WebHookEndpointConfig endpointConfig) =>
        FromWebHookEndpointConfig(endpointConfig, formId: null);

    /// <summary>
    /// Creates TaskInstructions from a WebHookEndpointConfig, naming the form whose settings hold the endpoint.
    /// </summary>
    /// <param name="endpointConfig">The webhook endpoint configuration from the database.</param>
    /// <param name="formId">The form whose settings name the endpoint; null for tenant or server settings.</param>
    /// <returns>A new TaskInstructions instance.</returns>
    public static TaskInstructions FromWebHookEndpointConfig(
        Endatix.Core.Entities.WebHookEndpointConfig endpointConfig,
        long? formId)
    {
        AuthenticationConfig? authConfig = null;

        if (endpointConfig.Authentication != null && endpointConfig.Authentication.Type != "None")
        {
            authConfig = new AuthenticationConfig
            {
                Type = endpointConfig.Authentication.Type == "ApiKey"
                    ? AuthenticationType.ApiKey
                    : AuthenticationType.None,
                ApiKey = endpointConfig.Authentication.ApiKey,
                ApiKeyHeader = endpointConfig.Authentication.ApiKeyHeader
            };
        }

        return new TaskInstructions(endpointConfig.Url, authConfig) { FormId = formId };
    }

    /// <summary>
    /// Gets the URI associated with the task instruction set.
    /// </summary>
    public string Uri { get; init; }

    /// <summary>
    /// Gets the authentication configuration for the webhook request.
    /// </summary>
    public AuthenticationConfig? Authentication { get; init; }

    /// <summary>
    /// Gets the form whose settings name the endpoint, for the log; null for tenant or server settings.
    /// </summary>
    public long? FormId { get; init; }
}
