using System.Text.Json;
using Endatix.Core.Events;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Outbox.Engine;

namespace Endatix.Modules.Reporting.Features.Outbox;

/// <summary>
/// Handles the form definition updated event by compiling the persisted form schema.
/// </summary>
/// <remarks>
/// The same work runs inline in the relay or as a background job; both run the work <see cref="Read"/> returns.
/// </remarks>
internal sealed class CompileFormSchemaOutboxHandler(IFormSchemaProcessor schemaProcessor)
    : IOutboxWork<CompileFormSchemaOutboxHandler>
{
    public static string JobType => "ReportingCompileFormSchema";

    public static IReadOnlyCollection<string> HandledEventTypes { get; } = [FormDefinitionUpdatedEvent.EventTypeName];

    /// <inheritdoc />
    public OutboxWork Read(IOutboxMessage message) =>
        OutboxWork.For(Parse(message), ProcessAsync);

    /// <summary>Reads the work from the message; throws <see cref="InvalidOperationException"/> when it cannot.</summary>
    public static Input Parse(IOutboxMessage message)
    {
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;

        return new Input(
            message.GetRequiredTenantId(payload),
            message.GetRequiredIdProp(payload, "formId"),
            message.GetRequiredIdProp(payload, "formDefinitionId"));
    }

    public Task ProcessAsync(Input input, CancellationToken cancellationToken) =>
        schemaProcessor.ProcessAsync(input.TenantId, input.FormId, input.FormDefinitionId, cancellationToken: cancellationToken);

    public sealed record Input(long TenantId, long FormId, long FormDefinitionId);
}
