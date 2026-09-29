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
    /// Reads <paramref name="payloadJson"/> as <typeparamref name="TPayload"/>.
    /// </summary>
    /// <exception cref="JsonException">
    /// The text is empty, is not JSON, is the literal <c>null</c>, or does not describe the type. Input that
    /// omits a required member or holds <c>null</c> for a non-nullable one counts as unreadable too, and so does
    /// any input when the payload type itself cannot be bound, such as a constructor parameter that matches no
    /// property. Stored input that cannot be read now never will be, so the caller treats this as a permanent
    /// failure.
    /// </exception>
    public static TPayload Deserialize<TPayload>(string payloadJson)
        where TPayload : IBackgroundJobPayload
    {
        try
        {
            return JsonSerializer.Deserialize<TPayload>(payloadJson, _options)
                ?? throw new JsonException($"The payload is null, not a {typeof(TPayload).Name}.");
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            // Raised for a payload type the reader cannot bind, such as a constructor parameter that matches no
            // property. Serializing such a type succeeds, so it is only found here; surfaced as JsonException so
            // callers catch one type and the job fails at once instead of retrying something no retry can fix.
            throw new JsonException(ex.Message, ex);
        }
    }
}
