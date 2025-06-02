using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Event dispatched when a form's enabled state changes.
/// </summary>
public sealed class FormEnabledStateChangedEvent(Form form, bool isEnabled) : DomainEventBase
{
    public Form Form { get; init; } = form;
    public bool IsEnabled { get; init; } = isEnabled;
}
