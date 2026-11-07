using Endatix.Core.Features.WebHooks;
using Endatix.Framework.Logging;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Source-generated logging for WebHook enqueueing, delivery and the background worker.
/// </summary>
internal static partial class WebHookLoggerExtensions
{
    [LoggerMessage(
        EventId = EndatixEventIds.Webhooks.EventSkipped,
        Level = LogLevel.Trace,
        Message = "WebHook for {EventName} event is disabled or not configured. Skipping processing...")]
    public static partial void LogWebHookEventSkipped(this ILogger logger, string eventName);

    [LoggerMessage(
        EventId = EndatixEventIds.Webhooks.NoEndpoints,
        Level = LogLevel.Trace,
        Message = "No webhook endpoints found for {EventName} event. Skipping processing...")]
    public static partial void LogWebHookNoEndpoints(this ILogger logger, string eventName);

    [LoggerMessage(
        EventId = EndatixEventIds.Webhooks.InvalidUri,
        Level = LogLevel.Error,
        Message = "Invalid WebHook URI: {Uri}. Skipping firing WebHook for {Operation} and Id: {Id}...")]
    public static partial void LogWebHookInvalidUri(this ILogger logger, string? uri, WebHookOperation operation, long id);

    [LoggerMessage(
        EventId = EndatixEventIds.Webhooks.Delivered,
        Level = LogLevel.Trace,
        Message = "Successfully processed WebHook for operation: {Operation}. Item id: {Id}. Status Code: {StatusCode}")]
    public static partial void LogWebHookDelivered(this ILogger logger, WebHookOperation operation, long id, System.Net.HttpStatusCode statusCode);

    [LoggerMessage(
        EventId = EndatixEventIds.Webhooks.DeliveryFailed,
        Level = LogLevel.Error,
        Message = "Failed to process WebHook for operation: {Operation}. Item id: {Id}. Status Code: {StatusCode}")]
    public static partial void LogWebHookDeliveryFailed(this ILogger logger, WebHookOperation operation, long id, System.Net.HttpStatusCode statusCode);

    [LoggerMessage(
        EventId = EndatixEventIds.Webhooks.DeliveryTimedOut,
        Level = LogLevel.Error,
        Message = "Webhook execution was cancelled due to timeout. Failed operation: {Operation}. Item id: {Id}. Destination: {Url}.")]
    public static partial void LogWebHookTimedOut(this ILogger logger, Exception exception, WebHookOperation operation, long id, string? url);

    [LoggerMessage(
        EventId = EndatixEventIds.Webhooks.DeliveryRejected,
        Level = LogLevel.Error,
        Message = "Webhook execution rejected because of timeout. Failed operation: {Operation}. Item id: {Id}. Destination: {Url}.")]
    public static partial void LogWebHookRejected(this ILogger logger, Exception exception, WebHookOperation operation, long id, string? url);

    [LoggerMessage(
        EventId = EndatixEventIds.Webhooks.DeliveryError,
        Level = LogLevel.Error,
        Message = "Error occurred while processing WebHook for operation: {Operation}. Item id: {Id}.")]
    public static partial void LogWebHookError(this ILogger logger, Exception exception, WebHookOperation operation, long id);

    [LoggerMessage(
        EventId = EndatixEventIds.Webhooks.WorkerStarted,
        Level = LogLevel.Information,
        Message = "{Service} Service is running.")]
    public static partial void LogWebHookWorkerStarted(this ILogger logger, string service);

    [LoggerMessage(
        EventId = EndatixEventIds.Webhooks.WorkerStopped,
        Level = LogLevel.Information,
        Message = "{Service} Service is stopped.")]
    public static partial void LogWebHookWorkerStopped(this ILogger logger, string service);
}
