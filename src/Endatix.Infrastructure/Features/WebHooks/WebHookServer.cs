using System.Text;
using System.Text.Json;
using Endatix.Core.Features.WebHooks;
using Microsoft.Extensions.Logging;
using Polly.Timeout;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Represents a server for firing WebHooks.
/// </summary>
internal class WebHookServer(HttpClient httpClient, ILogger<WebHookServer> logger)
{
    /// <summary>
    /// Fires a WebHook asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the payload carried by the WebHook message.</typeparam>
    /// <param name="message">The WebHook message to be sent.</param>
    /// <param name="instructions">Properties for the WebHook, including the URI.</param>
    /// <param name="token">A cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result indicates if the WebHook was successfully processed.</returns>
    internal async Task<bool> FireWebHookAsync<T>(WebHookMessage<T> message, WebHookProps instructions, CancellationToken token)
    {
        var isSuccess = false;
        try
        {
            if (instructions.Uri is null || !Uri.IsWellFormedUriString(instructions.Uri, UriKind.Absolute))
            {
                logger.LogError("Invalid WebHook URI: {uri}. Skipping firing WebHook for {operation} and Id: {id}...", instructions.Uri, message.Operation, message.Id);
                return false;
            }

            var jsonContent = JsonSerializer.Serialize(message);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(instructions.Uri, content, token);

            if (response.IsSuccessStatusCode)
            {
                logger.LogTrace($"Successfully processed WebHook for Submission. Status Code: {response.StatusCode}");
                isSuccess = true;
            }
            else
            {
                logger.LogError($"Failed to process WebHook. Status Code: {response.StatusCode}");
            }

        }
        catch (TaskCanceledException ex)
        {
            logger.LogError("Webhook execution was cancelled. Failed operation: {operation}.Item id: {id}. Destination: {url}. Error message: {message}.", message.Operation, message.Id, instructions.Uri, ex.Message);
        }
        catch (TimeoutRejectedException ex)
        {
            logger.LogError("Webhook execution rejected because of timeout. Failed operation: {operation}.Item id: {id}. Destination: {url}. Error message: {message}.", message.Operation, message.Id, instructions.Uri, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while processing WebHook @{message}.", message);
        }

        return isSuccess;
    }
}