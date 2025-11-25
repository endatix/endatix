using System.Text;
using System.Text.Json;
using Ardalis.GuardClauses;
using Endatix.Core.Features.WebHooks;
using Endatix.Framework.Serialization;
using Microsoft.Extensions.Logging;
using Polly.Timeout;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Represents a server for firing WebHooks.
/// </summary>
public class WebHookServer(HttpClient httpClient, ILogger<WebHookServer> logger)
{
    /// <summary>
    /// Fires a WebHook asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the payload carried by the WebHook message.</typeparam>
    /// <param name="message">The WebHook message to be sent.</param>
    /// <param name="instructions">Properties for the WebHook operation, including the destination URI.</param>
    /// <param name="token">A cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result indicates if the WebHook was successfully processed.</returns>
    internal async Task<bool> FireWebHookAsync<T>(WebHookMessage<T> message, TaskInstructions instructions, CancellationToken token)
    {
        var isSuccess = false;
        try
        {
            if (instructions.Uri is null || !Uri.IsWellFormedUriString(instructions.Uri, UriKind.Absolute))
            {
                logger.LogError("Invalid WebHook URI: {uri}. Skipping firing WebHook for {operation} and Id: {id}...", instructions.Uri, message.operation, message.id);
                return false;
            }

            var content = CreateContent(message);
            using var request = new HttpRequestMessage(HttpMethod.Post,instructions.Uri)
            {
                Content = content,
            };
            AddWebHookHeaders(request, message, instructions);

            var response = await httpClient.SendAsync(request, token);

            if (response.IsSuccessStatusCode)
            {
                logger.LogTrace($"Successfully processed WebHook for operation: {message.operation}. Item id: {message.id}. Status Code: {response.StatusCode}");
                isSuccess = true;
            }
            else
            {
                logger.LogError($"Failed to process WebHookfor operation: {message.operation}. Item id: {message.id}. Status Code: {response.StatusCode}");
            }

        }
         // This exception is thrown when a single request times out (configured with WebHookSettings's AttemptTimeoutInSeconds setting)
        catch (TaskCanceledException ex)
        {
            logger.LogError("Webhook execution was cancelled due to timeout. Failed operation: {operation}. Item id: {id}. Destination: {url}. Error message: {message}.", message.operation, message.id, instructions.Uri, ex.Message);
        }
        // This exception is thrown when the resilience pipeline cancels any further execution (max attempts or total timeout reached.)
        catch (TimeoutRejectedException ex)
        {
            logger.LogError("Webhook execution rejected because of timeout. Failed operation: {operation}.Item id: {id}. Destination: {url}. Error message: {message}.", message.operation, message.id, instructions.Uri, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while processing WebHook @{message}.", message);
        }

        return isSuccess;
    }


    private StringContent CreateContent<T>(WebHookMessage<T> message)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new LongToStringConverter());
        var jsonContent = JsonSerializer.Serialize(message, options);
        return new StringContent(jsonContent, Encoding.UTF8, "application/json");
    }


    private void AddWebHookHeaders<T>(HttpRequestMessage request, WebHookMessage<T> message, TaskInstructions instructions)
    {
        // Add standard Endatix webhook headers
        request.Headers.Add(WebHookRequestHeaders.Event, message.operation.EventName);
        request.Headers.Add(WebHookRequestHeaders.Entity, message.operation.Entity);
        request.Headers.Add(WebHookRequestHeaders.Action, message.operation.Action.GetDisplayName());
        request.Headers.Add(WebHookRequestHeaders.HookId, message.id.ToString());

        // Add authentication headers based on configuration
        AddAuthenticationHeaders(request, instructions.Authentication);
    }

    /// <summary>
    /// Adds authentication headers to the HTTP request based on the authentication configuration.
    /// </summary>
    /// <param name="request">The HTTP request message.</param>
    /// <param name="authConfig">The authentication configuration.</param>
    private void AddAuthenticationHeaders(HttpRequestMessage request, AuthenticationConfig? authConfig)
    {
        if (authConfig == null)
        {
            return;
        }

        switch (authConfig.Type)
        {
            case AuthenticationType.ApiKey:
                Guard.Against.NullOrWhiteSpace(authConfig.ApiKeyHeader);
                Guard.Against.NullOrWhiteSpace(authConfig.ApiKey);

                request.Headers.Add(authConfig.ApiKeyHeader, authConfig.ApiKey);
                break;

            case AuthenticationType.None:
            default:
                // No authentication headers to add
                break;
        }
    }
}
