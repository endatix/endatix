using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Event dispatched when a form is deleted.
/// </summary>
public sealed class FormDeletedEvent(Form form) : DomainEventBase
{
    public Form Form { get; init; } = form;
} 