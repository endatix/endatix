using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// In-process notification that a definition of the form was created or saved. Caches derived from the form's
/// definitions, such as the data lists its runtime may read, are evicted on it. Not an integration event.
/// </summary>
public sealed class FormDefinitionChangedEvent(long formId) : DomainEventBase
{
    /// <summary>The form whose definition changed.</summary>
    public long FormId { get; } = formId;
}
