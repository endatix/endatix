using Endatix.Core.Entities;

namespace Endatix.Core.Features.WebHooks;

/// <summary>
/// Represents a WebHook operation that can be performed on an entity.
/// </summary>
public record WebHookOperation
{
    /// <summary>
    /// A static instance of WebHookOperation representing a form creation.
    /// </summary>
    public static readonly WebHookOperation FormCreated = new("form_created", nameof(Form), ActionName.Created);

    /// <summary>
    /// A static instance of WebHookOperation representing a form update.
    /// </summary>
    public static readonly WebHookOperation FormUpdated = new("form_updated", nameof(Form), ActionName.Updated);

    /// <summary>
    /// A static instance of WebHookOperation representing a form enabled state change.
    /// </summary>
    public static readonly WebHookOperation FormEnabledStateChanged = new("form_enabled_state_changed", nameof(Form), ActionName.Updated);

    /// <summary>
    /// A static instance of WebHookOperation representing a form submission.
    /// </summary>
    public static readonly WebHookOperation FormSubmitted = new("form_submitted", nameof(Submission), ActionName.Created);

    /// <summary>
    /// Initializes a new instance of the WebHookOperation record.
    /// </summary>
    /// <param name="eventName">The name of the WebHook event.</param>
    /// <param name="entity">The entity on which the WebHook operation is performed.</param>
    /// <param name="action">The action performed on the entity.</param>
    private WebHookOperation(string eventName, string entity, ActionName action)
    {
        EventName = eventName;
        Entity = entity;
        Action = action;
    }

    /// <summary>
    /// Gets the name of the WebHook event.
    /// </summary>
    public string EventName { get; init; }

    /// <summary>
    /// Gets the entity on which the WebHook operation is performed.
    /// </summary>
    public string Entity { get; init; }

    /// <summary>
    /// Gets the action performed on the entity.
    /// </summary>
    public ActionName Action { get; init; }
}