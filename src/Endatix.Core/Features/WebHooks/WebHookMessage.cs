namespace Endatix.Core.Features.WebHooks;

/// <summary>
/// Represents a message to be sent via a WebHook.
/// </summary>
/// <typeparam name="TPayload">The type of the payload carried by the message.</typeparam>
public record WebHookMessage<TPayload>(long Id, WebHookOperation Operation, TPayload Payload)
{
    /// <summary>
    /// Gets or sets the unique identifier of the message.
    /// </summary>
    public long Id { get; init; } = Id;

    /// <summary>
    /// Gets or sets the display name of the action associated with the message.
    /// </summary>
    public string Action { get; set; } = Operation.Action.GetDisplayName();

    /// <summary>
    /// Gets or sets the operation associated with the message.
    /// </summary>
    public WebHookOperation Operation { get; init; } = Operation;

    /// <summary>
    /// Gets or sets the payload of the message.
    /// </summary>
    public TPayload Payload { get; init; } = Payload;
}