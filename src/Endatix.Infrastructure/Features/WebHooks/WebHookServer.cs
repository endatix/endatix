using System.Text;
using System.Text.Json;
using Endatix.Core.Features.WebHooks;
using Microsoft.Extensions.Logging;

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
        var isSuccess = false; // Flag to indicate if the WebHook was successfully processed
        try
        {
            // Serialize the WebHook message to JSON
            var jsonContent = JsonSerializer.Serialize(message);
            // Create a StringContent object with the serialized JSON and set the content type to application/json
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            // Post the content to the specified URI
            var response = await httpClient.PostAsync(instructions.Uri, content, token);

            // Check if the response was successful
            if (response.IsSuccessStatusCode)
            {
                // Log a trace message indicating the WebHook was successfully processed
                logger.LogTrace($"Successfully processed WebHook for Submission. Status Code: {response.StatusCode}");
                isSuccess = true; // Set the success flag to true
            }
            else
            {
                // Log a warning message if the WebHook processing failed
                logger.LogWarning($"Failed to process WebHook. Status Code: {response.StatusCode}");
            }

        }
        catch (Exception ex)
        {
            // Log an error message if an exception occurs during WebHook processing
            logger.LogError(ex, "Error occurred while processing WebHook @{message}.", message);
        }

        return isSuccess; // Return the success flag
    }
}