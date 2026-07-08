using System.Text.Json;
using Endatix.Infrastructure.Utils;
using Endatix.Outbox.Engine;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Reads integration-event payload properties with relay-friendly errors.
/// </summary>
public static class OutboxMessagePayloadExtensions
{
    /// <summary>
    /// Reads a required 64-bit integer id from a parsed payload object.
    /// </summary>
    /// <param name="message">The outbox message containing the payload.</param>
    /// <param name="payload">The JSON payload to read from.</param>
    /// <param name="propertyName">The name of the property to read.</param>
    /// <returns>The 64-bit integer value of the property.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the property is missing or not a valid id.</exception>
    public static long GetRequiredIdProp(this IOutboxMessage message, JsonElement payload, string propertyName)
    {
        var value = JsonElementReader.TryGetInt64(payload, propertyName);

        if (value is null)
        {
            throw new InvalidOperationException(
                $"Outbox message {message.Id} ({message.EventType}) is missing a valid {propertyName}.");
        }

        if (value.Value <= 0)
        {
            throw new InvalidOperationException(
                $"Outbox message {message.Id} ({message.EventType}) has an invalid {propertyName}.");
        }

        return value.Value;
    }

    /// <summary>
    /// Reads <c>tenantId</c> from the payload and verifies it matches the outbox row tenant.
    /// </summary>
    public static long GetRequiredTenantId(this IOutboxMessage message, JsonElement payload)
    {
        var tenantId = message.GetRequiredIdProp(payload, "tenantId");

        if (tenantId != message.TenantId)
        {
            throw new InvalidOperationException(
                $"Outbox message {message.Id} ({message.EventType}) tenantId mismatch: message={message.TenantId}, payload={tenantId}.");
        }

        return tenantId;
    }
}
