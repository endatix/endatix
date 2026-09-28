using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Endatix.Core.Abstractions.BackgroundJobs;

/// <summary>
/// The one serializer for job payloads, used both when a job is enqueued and when it runs, so the two
/// sides cannot drift apart.
/// </summary>
/// <remarks>
/// <para>
/// Web defaults, and no polymorphism, so a stored payload can only ever become the type its handler asks
/// for, and <see langword="long"/> ids stay JSON numbers. They only become strings at the HTTP boundary.
/// </para>
/// <para>
/// Reading is strict about what the payload type declares. A constructor parameter without a default value
/// must be present, and a non-nullable reference must not be <c>null</c>; otherwise the input is unreadable.
/// Without this, <c>{}</c> would read as a payload whose ids are all <c>0</c> and the job would run against
/// a record that was never named. Serializing a <c>null</c> into a non-nullable reference throws for the
/// same reason, so such a job is rejected when it is enqueued rather than when it runs.
/// </para>
/// </remarks>
public static class BackgroundJobPayloadSerializer
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
    };

    /// <summary>Serializes <paramref name="payload"/> into the text stored in the job row.</summary>
    public static string Serialize<TPayload>(TPayload payload)
        where TPayload : IBackgroundJobPayload =>
        JsonSerializer.Serialize(payload, _options);

    /// <summary>
    /// Reads <paramref name="payloadJson"/> as <typeparamref name="TPayload"/>. Returns <see langword="false"/>,
    /// rather than throwing, when the text is not JSON or does not describe the type: stored input that
    /// cannot be read now never will be, so the caller treats it as a permanent failure. Input that omits a
    /// required member or holds <c>null</c> for a non-nullable one counts as unreadable.
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
