using System.Text.Json.Serialization;

namespace Endatix.Core.Features.WebHooks;

/// <summary>
/// Represents a message to be sent via a WebHook.
/// </summary>
/// <typeparam name="TPayload">The type of the payload carried by the message.</typeparam>
public record WebHookMessage<TPayload>(long id, WebHookOperation operation, TPayload payload)
{
    /// <summary>
    /// Gets or sets the unique identifier of the message.
    /// </summary>
    public long id { get; init; } = id;

    /// <summary>
    /// Gets or sets the name of the event associated with the message.
    /// </summary>
    public string eventName { get; set; } = operation.EventName;

    /// <summary>
    /// Gets or sets the display name of the action associated with the message.
    /// </summary>
    public string action { get; set; } = operation.Action.GetDisplayName();

    /// <summary>
    /// Gets or sets the operation associated with the message.
    /// </summary>
    [JsonIgnore]
    public WebHookOperation operation { get; init; } = operation;

    /// <summary>
    /// Gets or sets the payload of the message.
    /// </summary>
    public TPayload payload { get; init; } = payload;
}