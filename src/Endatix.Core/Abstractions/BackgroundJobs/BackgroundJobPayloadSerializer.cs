using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Endatix.Core.Abstractions.BackgroundJobs;

/// <summary>
/// The one serializer for job payloads, used both when a job is enqueued and when it runs, so the two
/// sides cannot drift apart.
/// </summary>
/// <remarks>
/// Web defaults, and deliberately nothing else: no polymorphism, so a stored payload can only ever become
/// the type its handler asks for, and <see langword="long"/> ids stay JSON numbers. They only become
/// strings at the HTTP boundary.
/// </remarks>
public static class BackgroundJobPayloadSerializer
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    /// <summary>Serializes <paramref name="payload"/> into the text stored in the job row.</summary>
    public static string Serialize<TPayload>(TPayload payload)
        where TPayload : IBackgroundJobPayload =>
        JsonSerializer.Serialize(payload, _options);

    /// <summary>
    /// Reads <paramref name="payloadJson"/> as <typeparamref name="TPayload"/>. Returns <see langword="false"/>,
    /// rather than throwing, when the text is not JSON or does not describe the type: stored input that
    /// cannot be read now never will be, so the caller treats it as a permanent failure.
    /// </summary>
    public static bool TryDeserialize<TPayload>(
        string? payloadJson,
        [NotNullWhen(true)] out TPayload? payload)
        where TPayload : IBackgroundJobPayload
    {
        payload = default;

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            payload = JsonSerializer.Deserialize<TPayload>(payloadJson, _options);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }

        return payload is not null;
    }
}
